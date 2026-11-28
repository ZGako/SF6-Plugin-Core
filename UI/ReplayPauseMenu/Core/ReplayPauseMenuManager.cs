using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.ReplayPauseMenu.Hooks;
using SF6_Plugin_Core.UI.ReplayPauseMenu.Requests;

namespace SF6_Plugin_Core.UI.ReplayPauseMenu.Core;

/// <summary>
///     Manages queued UI modifications (reorders, element appends, and <c>OnDecideItem</c> hooks) over SF6's
///     Replay Pause Menu data slots (<c>app.UIReplayPauseMenuComp._MenuData</c>: slot <c>0</c> = Watch Replay,
///     slot <c>1</c> = Hijack/Takeover), restoring vanilla arrays cleanly on unregistration or teardown.
/// </summary>
public static class ReplayPauseMenuManager
{
    private static readonly Dictionary<int, SlotState> SlotStates = [];
    private static readonly Dictionary<IUIModificationRequest, int> RequestSlotMap = [];
    private static readonly Dictionary<ulong, MenuDataSnapshot> Snapshots = [];
    private static readonly Dictionary<app.esports.UIReplayPauseMenu.EReplayFuncType, ReplayDecideHookRequest>
        VanillaDecideHooks = [];
    private static readonly ConcurrentDictionary<Guid, Func<int>> SpinnerInitializers = [];
    private static readonly List<Action> PendingDisposals = [];
    private static readonly Lock ModificationLock = new();

    private static long _modificationVersion;
    private static volatile bool _hasActiveModifications;
    private static volatile bool _hasSnapshotsToRestore;
    private static volatile bool _isDirty;

    private static readonly Lazy<Field?> CompMenuDataFieldLazy =
        new(() => app.UIReplayPauseMenuComp.REFType?.GetField("_MenuData"));

    private static readonly Lazy<Field?> MenuDataItemsFieldLazy =
        new(() => app.ReplayPauseMenuData.REFType?.GetField("MenuItems"));

    private static Field? CompMenuDataField => CompMenuDataFieldLazy.Value;
    private static Field? MenuDataItemsField => MenuDataItemsFieldLazy.Value;

    internal static void RegisterSpinnerInitializer(Guid functionId, Func<int> getInitialIndex)
    {
        SpinnerInitializers[functionId] = getInitialIndex;
    }

    private static bool IsValidManagedObject(ManagedObject? mo)
    {
        if (mo == null) return false;
        var addr = mo.GetAddress();
        return addr != 0 && ManagedObject.IsManagedObject(addr);
    }

    private static void UpdateFlagsLocked()
    {
        _hasActiveModifications = SlotStates.Values.Any(static s => s.HasModifications);
        _hasSnapshotsToRestore = Snapshots.Count > 0;
    }

    private static void FlushPendingDisposals()
    {
        if (PendingDisposals.Count == 0) return;

        var disposals = PendingDisposals.ToArray();
        PendingDisposals.Clear();

        foreach (var disposeAction in disposals)
            try
            {
                disposeAction();
            }
            catch (Exception ex)
            {
                API.LogError($"[ReplayPauseMenuManager] Failed during deferred disposal: {ex}");
            }
    }

    /// <summary>
    ///     Registers a replay menu modification (<see cref="ReplayPauseMenuAppendElementsRequest" /> or
    ///     <see cref="ReorderRequest" />) for the Watch Replay menu (<see cref="ReplayPauseMenuSlot.WatchReplay" />).
    /// </summary>
    public static void RegisterReplayMenuModification(IUIModificationRequest request)
    {
        RegisterModification((int)ReplayPauseMenuSlot.WatchReplay, request);
    }

    /// <summary>
    ///     Registers a replay menu modification (<see cref="ReplayPauseMenuAppendElementsRequest" /> or
    ///     <see cref="ReorderRequest" />) for the Hijack / Takeover menu (<see cref="ReplayPauseMenuSlot.Hijack" />).
    /// </summary>
    public static void RegisterHijackMenuModification(IUIModificationRequest request)
    {
        RegisterModification((int)ReplayPauseMenuSlot.Hijack, request);
    }

    /// <summary>
    ///     Registers a replay menu modification (<see cref="ReplayPauseMenuAppendElementsRequest" /> or
    ///     <see cref="ReorderRequest" />) targeting a specific <c>_MenuData</c> <paramref name="slotId" />.
    /// </summary>
    public static void RegisterModification(ReplayPauseMenuSlot slot, IUIModificationRequest request)
    {
        RegisterModification((int)slot, request);
    }

    /// <summary>
    ///     Registers a replay menu modification (<see cref="ReplayPauseMenuAppendElementsRequest" /> or
    ///     <see cref="ReorderRequest" />) targeting <paramref name="slotId" /> inside
    ///     <c>app.UIReplayPauseMenuComp._MenuData</c>.
    /// </summary>
    public static void RegisterModification(int slotId, IUIModificationRequest request)
    {
        if (slotId < 0)
            throw new ArgumentOutOfRangeException(nameof(slotId), "Slot ID must be non-negative.");

        lock (ModificationLock)
        {
            if (request is ExclusiveModificationRequest exclusive)
                exclusive.Claim();

            if (!SlotStates.TryGetValue(slotId, out var slotState))
            {
                slotState = new SlotState();
                SlotStates[slotId] = slotState;
            }

            try
            {
                switch (request)
                {
                    case ReorderRequest reorderReq:
                        if (!slotState.RequestReorder(reorderReq))
                            throw new InvalidOperationException(
                                $"A ReorderRequest is already registered for replay menu slot {slotId}.");
                        break;
                    case ReplayPauseMenuAppendElementsRequest appendReq:
                        slotState.AppendRequests.Add(appendReq);
                        break;
                    default:
                        throw new ArgumentException(
                            $"Unsupported modification request type '{request.GetType().Name}'.",
                            nameof(request));
                }
            }
            catch
            {
                if (request is ExclusiveModificationRequest exclusiveToRelease)
                    exclusiveToRelease.ReleaseClaim();
                if (!slotState.HasModifications)
                    SlotStates.Remove(slotId);
                throw;
            }

            RequestSlotMap[request] = slotId;
            _modificationVersion++;
            UpdateFlagsLocked();
            _isDirty = true;
        }
    }

    /// <summary>
    ///     Attaches a decide hook onto an existing vanilla <see cref="app.esports.UIReplayPauseMenu.EReplayFuncType" />.
    /// </summary>
    public static void RegisterVanillaDecideHook(
        app.esports.UIReplayPauseMenu.EReplayFuncType funcType,
        ReplayDecideHookRequest request)
    {
        lock (ModificationLock)
        {
            if (VanillaDecideHooks.ContainsKey(funcType))
                throw new InvalidOperationException(
                    $"A decide hook is already registered for vanilla function type {funcType}.");

            request.Claim();
            try
            {
                var funcId = ReplayFuncTypeRegistry.RegisterGameFunctionType(funcType);
                request.AddHook(funcId);
                VanillaDecideHooks[funcType] = request;
            }
            catch
            {
                request.ReleaseClaim();
                throw;
            }
        }
    }

    /// <summary>
    ///     Unregisters a previously registered modification request (slot append/reorder or vanilla decide hook)
    ///     and schedules cleanup/array restoration.
    /// </summary>
    public static void UnregisterModification(IUIModificationRequest request)
    {
        lock (ModificationLock)
        {
            if (request is ReplayDecideHookRequest decideReq)
            {
                app.esports.UIReplayPauseMenu.EReplayFuncType? matchedType = null;
                foreach (var kvp in VanillaDecideHooks)
                    if (kvp.Value == decideReq)
                    {
                        matchedType = kvp.Key;
                        break;
                    }

                if (matchedType != null)
                {
                    decideReq.ClearHook();
                    decideReq.ReleaseClaim();
                    VanillaDecideHooks.Remove(matchedType.Value);
                    return;
                }
            }

            if (!RequestSlotMap.TryGetValue(request, out var slotId))
                return;

            RequestSlotMap.Remove(request);

            if (SlotStates.TryGetValue(slotId, out var slotState))
            {
                switch (request)
                {
                    case ReorderRequest reorderReq when slotState.ReorderRequest == reorderReq:
                        slotState.ReorderRequest = null;
                        break;
                    case ReplayPauseMenuAppendElementsRequest appendReq:
                        if (slotState.AppendRequests.Remove(appendReq))
                        {
                            foreach (var funcId in appendReq.GetElementFunctionIds())
                                SpinnerInitializers.TryRemove(funcId, out var ignored);

                            appendReq.ClearHooks();
                            PendingDisposals.Add(appendReq.ReleaseResources);
                        }

                        break;
                }

                if (!slotState.HasModifications)
                    SlotStates.Remove(slotId);
            }

            if (request is ExclusiveModificationRequest exclusive)
                exclusive.ReleaseClaim();

            _modificationVersion++;
            UpdateFlagsLocked();
            _isDirty = true;
        }
    }

    /// <summary>
    ///     Returns the captured original vanilla <c>MenuItems</c> array for <paramref name="slotId" /> once a
    ///     modification has snapshotted that <c>ReplayPauseMenuData</c>, or <see langword="null" /> if not yet seen.
    /// </summary>
    public static app.ReplayMenuItem_Array1D? GetOriginalMenuItems(int slotId)
    {
        lock (ModificationLock)
        {
            foreach (var snapshot in Snapshots.Values)
                if (snapshot.SlotId == slotId && IsValidManagedObject(snapshot.OriginalMenuItemsMo))
                    return snapshot.OriginalMenuItemsMo.As<app.ReplayMenuItem_Array1D>();
        }

        return null;
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.UIReplayPauseMenuComp.TryGetMenuData</c>.
    ///     Rebuilds modified <c>ReplayPauseMenuData.MenuItems</c> arrays on demand right before the game reads them.
    /// </summary>
    [MethodHook(typeof(app.UIReplayPauseMenuComp), "TryGetMenuData", MethodHookType.Pre)]
    public static PreHookResult OnTryGetMenuDataPre(Span<ulong> args)
    {
        if (!_hasActiveModifications && !_hasSnapshotsToRestore) return PreHookResult.Continue;
        if (args.Length < 3 || args[1] == 0) return PreHookResult.Continue;

        var slotId = (int)args[2];
        if (slotId < 0) return PreHookResult.Continue;

        lock (ModificationLock)
        {
            var hasSlotMod = SlotStates.TryGetValue(slotId, out var slotState) && slotState.HasModifications;
            if (!hasSlotMod && Snapshots.Count == 0)
                return PreHookResult.Continue;

            try
            {
                var compAddress = args[1];
                var menuDataArrayMo = CompMenuDataField?.GetDataBoxed(compAddress, false) as ManagedObject;
                if (!IsValidManagedObject(menuDataArrayMo)) return PreHookResult.Continue;

                var menuDataArray = menuDataArrayMo!.As<_System.Array>();
                if (menuDataArray == null || slotId >= menuDataArray.Length) return PreHookResult.Continue;

                var menuDataMo = menuDataArray.GetValue(slotId) as ManagedObject;
                if (!IsValidManagedObject(menuDataMo)) return PreHookResult.Continue;

                var menuDataAddr = menuDataMo!.GetAddress();
                var hasSnapshot = Snapshots.TryGetValue(menuDataAddr, out var snapshot);

                if (!hasSlotMod && !hasSnapshot)
                    return PreHookResult.Continue;

                if (hasSnapshot && snapshot!.AppliedVersion == _modificationVersion)
                    return PreHookResult.Continue;

                if (snapshot == null)
                {
                    if (!TryCreateSnapshot(menuDataMo, slotId, out snapshot))
                        return PreHookResult.Continue;

                    Snapshots[menuDataAddr] = snapshot!;
                }

                ApplySnapshotState(snapshot!, slotState);
                UpdateFlagsLocked();
                FlushPendingDisposals();
            }
            catch (Exception ex)
            {
                API.LogError($"[ReplayPauseMenuManager] Exception in OnTryGetMenuDataPre: {ex}");
            }
        }

        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.esports.UIReplayPauseMenu.Param.InitMenuList</c>.
    ///     Seeds <c>DefaultFocusIndex</c> for custom <c>Spin</c> items so they display their live setting on open.
    /// </summary>
    [MethodHook(typeof(app.esports.UIReplayPauseMenu.Param), "InitMenuList", MethodHookType.Pre)]
    public static PreHookResult OnInitMenuListPre(Span<ulong> args)
    {
        if (!_hasActiveModifications || SpinnerInitializers.IsEmpty) return PreHookResult.Continue;
        if (args.Length < 2 || args[1] == 0) return PreHookResult.Continue;

        try
        {
            var param = MemoryHelpers.GetAddressAs<app.esports.UIReplayPauseMenu.Param>(args[1]);
            var defaultFocus = param.DefaultFocusIndex;
            if (defaultFocus == null) return PreHookResult.Continue;

            foreach (var (funcId, getInitialIndex) in SpinnerInitializers)
                if (ReplayFuncTypeRegistry.TryGetFunctionType(funcId, out var funcTypeInt))
                    defaultFocus[funcTypeInt] = getInitialIndex();
        }
        catch (Exception ex)
        {
            API.LogError($"[ReplayPauseMenuManager] Exception in OnInitMenuListPre: {ex}");
        }

        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework post-<c>LateUpdateBehavior</c> callback applying queued updates or restorations to any
    ///     already-snapshotted <c>ReplayPauseMenuData</c> assets.
    /// </summary>
    [Callback(typeof(LateUpdateBehavior), CallbackType.Post)]
    public static void OnLateUpdateBehavior()
    {
        if (!_isDirty) return;

        lock (ModificationLock)
        {
            if (!_isDirty) return;
            _isDirty = false;

            if (Snapshots.Count > 0)
            {
                foreach (var (addr, snapshot) in Snapshots.ToArray())
                {
                    if (!IsValidManagedObject(snapshot.MenuDataMo))
                    {
                        ReleaseSnapshotHandles(snapshot);
                        Snapshots.Remove(addr);
                        continue;
                    }

                    if (snapshot.AppliedVersion == _modificationVersion)
                        continue;

                    SlotStates.TryGetValue(snapshot.SlotId, out var slotState);
                    ApplySnapshotState(snapshot, slotState);
                }

                UpdateFlagsLocked();
            }

            FlushPendingDisposals();
        }
    }

    /// <summary>
    ///     Restores all snapshotted <c>ReplayPauseMenuData</c> arrays if still valid and clears every registered
    ///     modification, hook, custom element resource, and type mapping.
    /// </summary>
    public static void ClearReplayPauseMenuHooks()
    {
        lock (ModificationLock)
        {
            foreach (var snapshot in Snapshots.Values)
            {
                if (IsValidManagedObject(snapshot.MenuDataMo) &&
                    IsValidManagedObject(snapshot.OriginalMenuItemsMo))
                    try
                    {
                        MenuDataItemsField?.SetDataBoxed(
                            snapshot.MenuDataMo.GetAddress(),
                            snapshot.OriginalMenuItemsMo,
                            false);
                    }
                    catch (Exception ex)
                    {
                        API.LogWarning($"[ReplayPauseMenuManager] Failed restoring MenuItems on clear: {ex.Message}");
                    }

                ReleaseSnapshotHandles(snapshot);
            }

            Snapshots.Clear();

            foreach (var slotState in SlotStates.Values)
            {
                foreach (var appendReq in slotState.AppendRequests)
                {
                    appendReq.Reset();
                    appendReq.ReleaseClaim();
                }

                slotState.AppendRequests.Clear();
                slotState.ReorderRequest = null;
            }

            SlotStates.Clear();
            RequestSlotMap.Clear();

            foreach (var decideReq in VanillaDecideHooks.Values)
            {
                decideReq.ClearHook();
                decideReq.ReleaseClaim();
            }

            VanillaDecideHooks.Clear();
            SpinnerInitializers.Clear();
            ReplayDecideHooks.Clear();
            ReplayFuncTypeRegistry.Clear();

            _modificationVersion++;
            _hasActiveModifications = false;
            _hasSnapshotsToRestore = false;
            _isDirty = false;

            FlushPendingDisposals();
        }
    }

    private static bool TryCreateSnapshot(ManagedObject menuDataMo, int slotId, out MenuDataSnapshot? snapshot)
    {
        snapshot = null;

        var itemsField = MenuDataItemsField;
        if (itemsField == null) return false;

        var originalItemsMo = itemsField.GetDataBoxed(menuDataMo.GetAddress(), false) as ManagedObject;
        if (!IsValidManagedObject(originalItemsMo)) return false;

        menuDataMo.Globalize();
        originalItemsMo!.Globalize();

        snapshot = new MenuDataSnapshot(menuDataMo, originalItemsMo, slotId);
        return true;
    }

    private static void ApplySnapshotState(MenuDataSnapshot snapshot, SlotState? slotState)
    {
        var itemsField = MenuDataItemsField;
        if (itemsField == null) return;

        var menuDataAddr = snapshot.MenuDataMo.GetAddress();

        if (slotState == null || !slotState.HasModifications)
        {
            if (IsValidManagedObject(snapshot.OriginalMenuItemsMo))
                itemsField.SetDataBoxed(menuDataAddr, snapshot.OriginalMenuItemsMo, false);

            var prevBuilt = snapshot.CurrentBuiltMenuItemsMo;
            snapshot.CurrentBuiltMenuItemsMo = null;
            if (prevBuilt != null)
                PendingDisposals.Add(prevBuilt.ReleaseIfGlobalized);

            Snapshots.Remove(menuDataAddr);
            var menuDataMo = snapshot.MenuDataMo;
            var origMo = snapshot.OriginalMenuItemsMo;
            PendingDisposals.Add(() =>
            {
                origMo.ReleaseIfGlobalized();
                menuDataMo.ReleaseIfGlobalized();
            });
            return;
        }

        if (!IsValidManagedObject(snapshot.OriginalMenuItemsMo))
            return;

        var origSysArray = snapshot.OriginalMenuItemsMo.As<_System.Array>();
        if (origSysArray == null) return;

        var originalLength = origSysArray.Length;
        var mergedBuckets = new List<app.ReplayMenuItem>[originalLength + 2];
        var addedCount = 0;

        foreach (var appendReq in slotState.AppendRequests)
        {
            var reqBuckets = appendReq.GetAppendList(originalLength);
            for (var i = 0; i < reqBuckets.Length; i++)
            {
                var bucket = reqBuckets[i];
                if (bucket == null || bucket.Count == 0) continue;

                mergedBuckets[i] ??= [];
                mergedBuckets[i].AddRange(bucket);
                addedCount += bucket.Count;
            }
        }

        var totalCount = originalLength + addedCount;
        var newArrayMo = app.ReplayMenuItem.REFType.CreateManagedArray((uint)totalCount);
        newArrayMo.Globalize();

        var newArray = newArrayMo.As<app.ReplayMenuItem_Array1D>();
        var writeIdx = 0;

        if (mergedBuckets[0] != null)
            foreach (var item in mergedBuckets[0])
                newArray[writeIdx++] = item;

        var reorderReq = slotState.ReorderRequest;
        for (var pos = 0; pos < originalLength; pos++)
        {
            var sourceIndex = reorderReq != null ? reorderReq.GetMapping(pos) : pos;
            var origItem = (origSysArray.GetValue(sourceIndex) as ManagedObject)?.As<app.ReplayMenuItem>();
            if (origItem != null)
                newArray[writeIdx++] = origItem;

            var afterBucket = mergedBuckets[sourceIndex + 1];
            if (afterBucket != null)
                foreach (var item in afterBucket)
                    newArray[writeIdx++] = item;
        }

        var bottomBucket = mergedBuckets[originalLength + 1];
        if (bottomBucket != null)
            foreach (var item in bottomBucket)
                newArray[writeIdx++] = item;

        if (writeIdx != totalCount)
        {
            newArrayMo.ReleaseIfGlobalized();
            throw new InvalidOperationException(
                $"[ReplayPauseMenuManager] Rebuilt array length mismatch (expected {totalCount}, wrote {writeIdx}).");
        }

        itemsField.SetDataBoxed(menuDataAddr, newArrayMo, false);

        var previousBuiltMo = snapshot.CurrentBuiltMenuItemsMo;
        if (previousBuiltMo != null)
            PendingDisposals.Add(previousBuiltMo.ReleaseIfGlobalized);

        snapshot.CurrentBuiltMenuItemsMo = newArrayMo;
        snapshot.AppliedVersion = _modificationVersion;
    }

    private static void ReleaseSnapshotHandles(MenuDataSnapshot snapshot)
    {
        snapshot.CurrentBuiltMenuItemsMo?.ReleaseIfGlobalized();
        snapshot.CurrentBuiltMenuItemsMo = null;
        snapshot.OriginalMenuItemsMo.ReleaseIfGlobalized();
        snapshot.MenuDataMo.ReleaseIfGlobalized();
    }

    private sealed class SlotState
    {
        public readonly List<ReplayPauseMenuAppendElementsRequest> AppendRequests = [];
        public ReorderRequest? ReorderRequest;

        public bool HasModifications => AppendRequests.Count > 0 || ReorderRequest != null;

        public bool RequestReorder(ReorderRequest reorderRequest)
        {
            if (ReorderRequest != null) return false;
            ReorderRequest = reorderRequest;
            return true;
        }
    }

    private sealed class MenuDataSnapshot(
        ManagedObject menuDataMo,
        ManagedObject originalMenuItemsMo,
        int slotId)
    {
        public ManagedObject MenuDataMo { get; } = menuDataMo;
        public ManagedObject OriginalMenuItemsMo { get; } = originalMenuItemsMo;
        public ManagedObject? CurrentBuiltMenuItemsMo { get; set; }
        public int SlotId { get; } = slotId;
        public long AppliedVersion { get; set; } = -1;
    }
}
