// Core usings

using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

/// <summary>
///     Handles injection of custom (or duplicated) shortcuts into the game's shortcut settings.
///     <para>
///         Every change is queued and applied on <see cref="LateUpdateBehavior" />: the original
///         <c>_SettingUserData.Data</c> array is restored, previously injected save items and any entries with
///         non-pre-existing shortcut types are pruned, and then every active registration is re-appended.
///     </para>
/// </summary>
public static class ShortcutManager
{
    private static readonly Lock ShortcutLock = new();

    // Registrations requested by users, in registration order
    private static readonly List<ShortcutRegistration> ActiveRegistrations = [];

    // Registrations whose instances are currently injected in the game's data
    private static readonly List<ShortcutRegistration> AppliedRegistrations = [];

    // Shortcut type -> callbacks, refilled on every rebuild
    private static readonly ConcurrentDictionary<int, Action[]> TriggerActions = [];
    private static readonly ConcurrentDictionary<int, Action[]> UpdateActions = [];
    private static readonly ConcurrentDictionary<int, Action[]> ReleaseActions = [];

    private static readonly Lazy<HashSet<int>> PreExistingShortcutTypesLazy = new(ComputePreExistingShortcutTypes);
    private static readonly Lazy<int> ShortcutTypeMaxLazy = new(ComputeShortcutTypeMax);

    private static ManagedObject? _originalSettingArrayMo; // globalized copy of the original Data array
    private static ManagedObject? _injectedSettingArrayMo; // globalized array currently assigned by us
    private static int _customTypeCount;
    private static volatile bool _isDirty;
    private static volatile bool _hasApplied;

    /// <summary>
    ///     Highest value declared in <c>app.ShortcutSettingData.EShortcutType</c>. Custom shortcuts are assigned
    ///     <c>ShortcutTypeMax + n</c>.
    /// </summary>
    public static int ShortcutTypeMax => ShortcutTypeMaxLazy.Value;

    #region Public API

    /// <summary>
    ///     Queues a shortcut to be injected on the next <see cref="LateUpdateBehavior" />.
    /// </summary>
    /// <param name="node">The shortcut definition (setting data + save data).</param>
    /// <param name="onTrigger">Called from <c>app.ShortcutSetting.onShortcutTrigger</c> for the assigned type.</param>
    /// <param name="onUpdate">Called from <c>app.ShortcutSetting.onShortcutUpdate</c> for the assigned type.</param>
    /// <param name="onRelease">Called from <c>app.ShortcutSetting.onShortcutRelease</c> for the assigned type.</param>
    /// <param name="onUnregister">
    ///     Called once the shortcut is unregistered (explicitly or on plugin unload), with the node updated to the live
    ///     save data values (Button, Key, IsFunc, ...). Use it to persist the user's bindings in your own config.
    /// </param>
    /// <returns>A handle to pass to <see cref="UnregisterShortcut" />.</returns>
    public static Guid RegisterShortcut(
        CustomShortcutNode node,
        Action? onTrigger = null,
        Action? onUpdate = null,
        Action? onRelease = null,
        Action<CustomShortcutNode>? onUnregister = null)
    {
        var registration = new ShortcutRegistration(Guid.NewGuid(), node, onTrigger, onUpdate, onRelease, onUnregister);

        lock (ShortcutLock)
        {
            ActiveRegistrations.Add(registration);
            _isDirty = true;
        }

        return registration.Id;
    }

    /// <summary>
    ///     Queues removal of a previously registered shortcut. Captures live save values first and invokes
    ///     <c>onUnregister</c> immediately before stripping the injected entry on the next rebuild.
    /// </summary>
    /// <param name="id">The registration handle returned by <see cref="RegisterShortcut" />.</param>
    /// <returns><see langword="true" /> if the shortcut was registered; otherwise <see langword="false" />.</returns>
    public static bool UnregisterShortcut(Guid id)
    {
        ShortcutRegistration registration;

        lock (ShortcutLock)
        {
            var index = ActiveRegistrations.FindIndex(r => r.Id == id);
            if (index < 0) return false;

            registration = ActiveRegistrations[index];
            ActiveRegistrations.RemoveAt(index);

            // Record the live values before the rebuild strips the save item from the game's list
            registration.CaptureLiveSaveData();

            // Never injected: nothing for the rebuild to clean up, release right away
            if (!AppliedRegistrations.Contains(registration)) registration.Release();

            _isDirty = true;
        }

        InvokeUnregisterCallback(registration);
        return true;
    }

    #endregion

    #region Rebuild

    /// <summary>
    ///     REFramework post-<c>LateUpdateBehavior</c> callback (not intended for direct calls by mods). Waits until
    ///     <see cref="GameSingletonManager.ShortcutSetting" /> and its arrays are populated, then applies pending
    ///     registrations/removals or re-applies if the game reset <c>_SettingUserData.Data</c> or <c>ItemDataList</c>.
    /// </summary>
    [Callback(typeof(LateUpdateBehavior), CallbackType.Post)]
    public static void OnLateUpdateBehavior()
    {
        if (!_isDirty && !_hasApplied) return;

        lock (ShortcutLock)
        {
            if (!_isDirty && !_hasApplied) return;

            // Keep the requests queued until the shortcut settings exist
            var shortcutSetting = GameSingletonManager.ShortcutSetting;
            if (shortcutSetting == null) return;

            if (!_isDirty && !NeedsReapply(shortcutSetting)) return;

            // Wait until both the setting array and save list are populated before injecting
            if (ActiveRegistrations.Count > 0 && !IsShortcutDataReady(shortcutSetting)) return;

            _isDirty = false;
            try
            {
                RebuildShortcuts(shortcutSetting);
            }
            catch (Exception ex)
            {
                API.LogError($"[ShortcutManager] Failed to rebuild shortcuts: {ex}");
            }
        }
    }

    private static void RestoreAppliedData(app.ShortcutSetting shortcutSetting, bool releaseInactive)
    {
        var settingUserData = shortcutSetting._SettingUserData;
        var itemDataList = shortcutSetting.SettingSaveData?.ItemDataList;

        // 1. Record live save data of still-active shortcuts and strip every injected save item from the list
        // (the game may already have released the list/items during teardown, so everything is guarded)
        foreach (var registration in AppliedRegistrations)
        {
            var isStillActive = ActiveRegistrations.Contains(registration);

            try
            {
                if (isStillActive) registration.CaptureLiveSaveData();

                if (itemDataList != null && registration.SaveDataMo != null)
                {
                    var saveItem = registration.SaveDataMo.As<app.ShortcutSaveData.ShortcutSettingItemSaveData>();
                    if (saveItem != null) itemDataList.Remove(saveItem);
                }
            }
            catch (Exception ex)
            {
                API.LogWarning($"[ShortcutManager] Failed to remove save item during restore: {ex.Message}");
            }

            if (releaseInactive && !isStillActive) registration.Release();
        }

        AppliedRegistrations.Clear();
        _hasApplied = false;

        // 2. Prune any remaining save items whose shortcut type does not pre-exist in the game
        if (itemDataList != null)
            try
            {
                for (var i = itemDataList.Count - 1; i >= 0; i--)
                {
                    var saveItem = itemDataList[i];
                    if (saveItem != null && !IsPreExistingShortcutType(saveItem.ShortCutType))
                        itemDataList.RemoveAt(i);
                }
            }
            catch (Exception ex)
            {
                API.LogWarning($"[ShortcutManager] Failed to prune unknown save items during restore: {ex.Message}");
            }

        // 3. Restore the original setting data array and prune any non-pre-existing types
        if (settingUserData != null)
        {
            if (_originalSettingArrayMo != null)
            {
                var originalArray = _originalSettingArrayMo.As<app.ShortcutSettingData_Array1D>();
                if (originalArray != null) settingUserData.Data = originalArray;
            }

            try
            {
                PruneUnknownSettingData(settingUserData);
            }
            catch (Exception ex)
            {
                API.LogWarning(
                    $"[ShortcutManager] Failed to prune unknown setting data during restore: {ex.Message}");
            }
        }

        _injectedSettingArrayMo?.ReleaseIfGlobalized();
        _injectedSettingArrayMo = null;

        // 4. Reset the type allocation and the dispatch tables
        _customTypeCount = 0;
        TriggerActions.Clear();
        UpdateActions.Clear();
        ReleaseActions.Clear();
    }

    private static void RebuildShortcuts(app.ShortcutSetting shortcutSetting)
    {
        if (!ShortcutLock.IsHeldByCurrentThread)
            throw new SynchronizationLockException("RebuildShortcuts must be called while holding ShortcutLock.");

        // ------------------------------------------
        // RESTORE PHASE
        // ------------------------------------------
        RestoreAppliedData(shortcutSetting, true);

        if (ActiveRegistrations.Count == 0)
        {
            // Fully restored, no need to keep the original cached anymore
            _originalSettingArrayMo?.ReleaseIfGlobalized();
            _originalSettingArrayMo = null;
            return;
        }

        // ------------------------------------------
        // APPLY PHASE
        // ------------------------------------------
        var settingUserData = shortcutSetting._SettingUserData;
        var itemDataList = shortcutSetting.SettingSaveData?.ItemDataList;

        if (settingUserData == null)
        {
            API.LogWarning("[ShortcutManager] _SettingUserData is null. Cannot inject shortcuts.");
            return;
        }

        _originalSettingArrayMo ??= CopySettingArray(settingUserData.Data);

        if (itemDataList == null)
            API.LogWarning("[ShortcutManager] SettingSaveData.ItemDataList is null. Save items will not be injected.");

        var originalArr = _originalSettingArrayMo.As<app.ShortcutSettingData_Array1D>();
        var originalLength = originalArr.Length;

        var newArrMo = app.ShortcutSettingData.REFType.CreateManagedArray(
            (uint)(originalLength + ActiveRegistrations.Count));
        newArrMo.Globalize();
        var newArr = newArrMo.As<app.ShortcutSettingData_Array1D>();

        for (var i = 0; i < originalLength; i++) newArr[i] = originalArr[i];

        var newIndex = originalLength;
        foreach (var registration in ActiveRegistrations)
        {
            var shortcutType = registration.Node.ShortcutType is { } existingType
                ? (int)existingType
                : ShortcutTypeMax + ++_customTypeCount;

            newArr[newIndex++] = registration.BuildSettingData(shortcutType);

            var saveData = registration.BuildSaveData(shortcutType);
            itemDataList?.Add(saveData);

            AddAction(TriggerActions, shortcutType, registration.OnTrigger);
            AddAction(UpdateActions, shortcutType, registration.OnUpdate);
            AddAction(ReleaseActions, shortcutType, registration.OnRelease);

            AppliedRegistrations.Add(registration);
        }

        settingUserData.Data = newArr;
        _injectedSettingArrayMo = newArrMo;
        _hasApplied = AppliedRegistrations.Count > 0;
    }

    private static bool IsShortcutDataReady(app.ShortcutSetting shortcutSetting)
    {
        try
        {
            var settingData = shortcutSetting._SettingUserData?.Data;
            if (settingData == null || settingData.Length == 0) return false;

            var itemDataList = shortcutSetting.SettingSaveData?.ItemDataList;
            return itemDataList is { Count: > 0 };
        }
        catch
        {
            return false;
        }
    }

    private static bool NeedsReapply(app.ShortcutSetting shortcutSetting)
    {
        if (AppliedRegistrations.Count == 0) return false;

        try
        {
            var settingData = shortcutSetting._SettingUserData?.Data;
            var itemDataList = shortcutSetting.SettingSaveData?.ItemDataList;
            if (settingData == null || itemDataList == null || itemDataList.Count == 0) return false;

            if (_originalSettingArrayMo != null)
            {
                var originalArr = _originalSettingArrayMo.As<app.ShortcutSettingData_Array1D>();
                if (originalArr != null && settingData.Length != originalArr.Length + AppliedRegistrations.Count)
                    return true;
            }

            foreach (var registration in AppliedRegistrations)
            {
                if (registration.SaveDataMo == null) return true;
                var saveItem = registration.SaveDataMo.As<app.ShortcutSaveData.ShortcutSettingItemSaveData>();
                if (saveItem == null || !itemDataList.Contains(saveItem)) return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    ///     Unregisters every shortcut (invoking their <c>onUnregister</c> callbacks with live data), restores the
    ///     original game data and releases all cached objects. Called automatically right before
    ///     <c>app.battle.TrainingFlowMap.onEnd</c> runs.
    /// </summary>
    internal static void ClearShortcuts()
    {
        List<ShortcutRegistration> removed;

        lock (ShortcutLock)
        {
            removed = [.. ActiveRegistrations];
            foreach (var registration in removed)
                try
                {
                    registration.CaptureLiveSaveData();
                }
                catch (Exception ex)
                {
                    API.LogWarning($"[ShortcutManager] Failed to capture live save data: {ex.Message}");
                }

            ActiveRegistrations.Clear();

            // With no active registrations, a rebuild restores the original data, prunes unknown types, and releases the cache
            var shortcutSetting = GameSingletonManager.ShortcutSetting;
            if (shortcutSetting != null)
                try
                {
                    RebuildShortcuts(shortcutSetting);
                }
                catch (Exception ex)
                {
                    API.LogError($"[ShortcutManager] Failed to restore shortcut data: {ex}");
                }

            // Fallback cleanup in case the game data was unreachable
            foreach (var registration in AppliedRegistrations) registration.Release();
            AppliedRegistrations.Clear();
            _hasApplied = false;
            foreach (var registration in removed) registration.Release();

            _injectedSettingArrayMo?.ReleaseIfGlobalized();
            _injectedSettingArrayMo = null;
            _originalSettingArrayMo?.ReleaseIfGlobalized();
            _originalSettingArrayMo = null;

            _customTypeCount = 0;
            TriggerActions.Clear();
            UpdateActions.Clear();
            ReleaseActions.Clear();
            _isDirty = false;
        }

        foreach (var registration in removed) InvokeUnregisterCallback(registration);
    }

    #endregion

    #region Hooks

    /// <summary>
    ///     Restores vanilla shortcut data before <c>app.ShortcutSetting.Init</c> runs so the game does not
    ///     initialize default save items for our injected shortcuts or overwrite them in place.
    /// </summary>
    [MethodHook(typeof(app.ShortcutSetting), "Init", MethodHookType.Pre)]
    public static PreHookResult OnShortcutInitPre(Span<ulong> args)
    {
        lock (ShortcutLock)
        {
            if (AppliedRegistrations.Count > 0 || _originalSettingArrayMo != null)
            {
                var shortcutSetting = GameSingletonManager.ShortcutSetting ??
                                      API.GetManagedSingletonT<app.ShortcutSetting>();
                if (shortcutSetting != null)
                    try
                    {
                        RestoreAppliedData(shortcutSetting, false);
                    }
                    catch (Exception ex)
                    {
                        API.LogWarning($"[ShortcutManager] Failed to restore shortcut data before Init: {ex.Message}");
                    }

                _originalSettingArrayMo?.ReleaseIfGlobalized();
                _originalSettingArrayMo = null;
            }

            if (ActiveRegistrations.Count > 0) _isDirty = true;
        }

        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework post-hook on <c>app.ShortcutSetting.Init</c> (not intended for direct calls by mods). Marks
    ///     pending custom shortcuts dirty so they re-inject once initialization finishes.
    /// </summary>
    /// <param name="retval">Raw return value from the hooked method (unused).</param>
    [MethodHook(typeof(app.ShortcutSetting), "Init", MethodHookType.Post)]
    public static void OnShortcutInitPost(ref ulong retval)
    {
        lock (ShortcutLock)
        {
            if (ActiveRegistrations.Count > 0) _isDirty = true;
        }
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.training.TrainingManager.Save(System.Int32, System.Action)</c> (not
    ///     intended for direct calls by mods). Restores vanilla shortcut data before training saves or tears down.
    /// </summary>
    /// <param name="args">Raw VM argument span (unused).</param>
    /// <returns>Always <see cref="PreHookResult.Continue" />.</returns>
    [MethodHook(typeof(app.training.TrainingManager),
        "Save(System.Int32, System.Action)", MethodHookType.Pre)]
    public static PreHookResult OnTrainingFlowMapEndPre(Span<ulong> args)
    {
        ClearShortcuts();
        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.ShortcutSetting.onShortcutTrigger</c> (not intended for direct calls by
    ///     mods). Dispatches <c>onTrigger</c> callbacks registered for the triggered shortcut type.
    /// </summary>
    /// <param name="args">Raw VM argument span where <c>args[2]</c> is the integer <c>EShortcutType</c>.</param>
    /// <returns>Always <see cref="PreHookResult.Continue" />.</returns>
    [MethodHook(typeof(app.ShortcutSetting), "onShortcutTrigger(app.ShortcutSettingData.EShortcutType)",
        MethodHookType.Pre)]
    public static PreHookResult OnShortcutTriggerPre(Span<ulong> args)
    {
        Dispatch(TriggerActions, (int)args[2]);
        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.ShortcutSetting.onShortcutUpdate</c> (not intended for direct calls by
    ///     mods). Dispatches held/frame-update <c>onUpdate</c> callbacks registered for the shortcut type.
    /// </summary>
    /// <param name="args">Raw VM argument span where <c>args[2]</c> is the integer <c>EShortcutType</c>.</param>
    /// <returns>Always <see cref="PreHookResult.Continue" />.</returns>
    [MethodHook(typeof(app.ShortcutSetting), "onShortcutUpdate(app.ShortcutSettingData.EShortcutType)",
        MethodHookType.Pre)]
    public static PreHookResult OnShortcutUpdatePre(Span<ulong> args)
    {
        Dispatch(UpdateActions, (int)args[2]);
        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.ShortcutSetting.onShortcutRelease</c> (not intended for direct calls by
    ///     mods). Dispatches <c>onRelease</c> callbacks registered for the released shortcut type.
    /// </summary>
    /// <param name="args">Raw VM argument span where <c>args[2]</c> is the integer <c>EShortcutType</c>.</param>
    /// <returns>Always <see cref="PreHookResult.Continue" />.</returns>
    [MethodHook(typeof(app.ShortcutSetting), "onShortcutRelease(app.ShortcutSettingData.EShortcutType)",
        MethodHookType.Pre)]
    public static PreHookResult OnShortcutReleasePre(Span<ulong> args)
    {
        Dispatch(ReleaseActions, (int)args[2]);
        return PreHookResult.Continue;
    }

    private static void Dispatch(ConcurrentDictionary<int, Action[]> actions, int shortcutType)
    {
        if (actions.IsEmpty || !actions.TryGetValue(shortcutType, out var callbacks)) return;

        foreach (var callback in callbacks)
            try
            {
                callback();
            }
            catch (Exception ex)
            {
                API.LogError($"[ShortcutManager] Shortcut callback for type {shortcutType} threw: {ex}");
            }
    }

    #endregion

    #region Helpers

    private static void AddAction(ConcurrentDictionary<int, Action[]> actions, int shortcutType, Action? action)
    {
        if (action == null) return;
        actions.AddOrUpdate(shortcutType, [action], (_, existing) => [.. existing, action]);
    }

    private static ManagedObject CopySettingArray(app.ShortcutSettingData_Array1D? source)
    {
        var length = source?.Length ?? 0;

        var copyMo = app.ShortcutSettingData.REFType.CreateManagedArray((uint)length);
        copyMo.Globalize();
        var copy = copyMo.As<app.ShortcutSettingData_Array1D>();

        for (var i = 0; i < length; i++) copy[i] = source![i];

        return copyMo;
    }

    private static void PruneUnknownSettingData(app.ShortcutSettingUserData settingUserData)
    {
        var data = settingUserData.Data;
        if (data == null) return;

        var length = data.Length;
        var validCount = 0;
        for (var i = 0; i < length; i++)
        {
            var entry = data[i];
            if (entry == null || IsPreExistingShortcutType(entry.ShortCutType)) validCount++;
        }

        if (validCount == length) return;

        var prunedMo = app.ShortcutSettingData.REFType.CreateManagedArray((uint)validCount);
        var pruned = prunedMo.As<app.ShortcutSettingData_Array1D>();
        var writeIndex = 0;
        for (var i = 0; i < length; i++)
        {
            var entry = data[i];
            if (entry == null || IsPreExistingShortcutType(entry.ShortCutType)) pruned[writeIndex++] = entry!;
        }

        settingUserData.Data = pruned;

        // Keep the cached original array in sync if it contained non-pre-existing types
        if (_originalSettingArrayMo == null) return;

        _originalSettingArrayMo.ReleaseIfGlobalized();
        prunedMo.Globalize();
        _originalSettingArrayMo = prunedMo;
    }

    private static bool IsPreExistingShortcutType(app.ShortcutSettingData.EShortcutType shortcutType)
    {
        var preExistingTypes = PreExistingShortcutTypesLazy.Value;
        return preExistingTypes.Count == 0 || preExistingTypes.Contains((int)shortcutType);
    }

    private static HashSet<int> ComputePreExistingShortcutTypes()
    {
        var values = new HashSet<int>();

        var typeDef = API.GetTDB().FindType("app.ShortcutSettingData.EShortcutType");
        if (typeDef != null)
            foreach (var field in typeDef.GetFields().Where(field => field.IsStatic() && field.IsLiteral()))
            {
                var rawPtr = field.GetDataRaw(0, false);
                if (rawPtr != 0) values.Add(Marshal.ReadInt32((nint)rawPtr));
            }

        if (values.Count > 0) return values;

        // Fallback to the generated enum if the TDB lookup failed
        API.LogWarning("[ShortcutManager] EShortcutType not found in TDB, falling back to the generated enum.");
        foreach (var value in Enum.GetValues<app.ShortcutSettingData.EShortcutType>())
            values.Add(Convert.ToInt32(value));

        return values;
    }

    private static int ComputeShortcutTypeMax()
    {
        return PreExistingShortcutTypesLazy.Value.Count > 0
            ? PreExistingShortcutTypesLazy.Value.Max()
            : int.MinValue;
    }

    private static void InvokeUnregisterCallback(ShortcutRegistration registration)
    {
        if (registration.OnUnregister == null) return;

        try
        {
            registration.OnUnregister(registration.Node);
        }
        catch (Exception ex)
        {
            API.LogError($"[ShortcutManager] onUnregister callback threw: {ex}");
        }
    }

    #endregion

    #region Types

    /// <summary>
    ///     Combined definition of <c>app.ShortcutSettingData</c> and
    ///     <c>app.ShortcutSaveData.ShortcutSettingItemSaveData</c>.
    /// </summary>
    public readonly record struct CustomShortcutNode
    {
        // ---- Shared ----

        /// <summary>
        ///     <c>null</c> (default): a new custom type is allocated (<see cref="ShortcutTypeMax" /> + n).
        ///     <para>Set: an additional (duplicate) entry is added using this existing type.</para>
        /// </summary>
        public app.ShortcutSettingData.EShortcutType? ShortcutType { get; init; }

        // ---- app.ShortcutSettingData ----

        /// <summary>Localized display name shown for this shortcut row in SF6's Shortcut Settings menu.</summary>
        public CustomMessage? ItemMessage { get; init; }

        /// <summary>Localized help text shown in the bottom guide area when this shortcut is selected.</summary>
        public CustomMessage? GuideMessage { get; init; }

        /// <summary>Fallback gamepad button applied when resetting shortcuts to defaults.</summary>
        public via.hid.GamePadButton DefaultButton { get; init; }

        /// <summary>Fallback keyboard key applied when resetting shortcuts to defaults.</summary>
        public via.hid.KeyboardKey DefaultKey { get; init; }

        /// <summary>Default state of the Function modifier requirement (e.g. requiring the Training Function button).</summary>
        public bool IsDefaultFunc { get; init; }

        /// <summary>Whether keyboard bindings are permitted alongside gamepad bindings in the shortcut menu.</summary>
        public bool IsKeyInput { get; init; }

        /// <summary>Whether the shortcut entry starts enabled when initialized or reset to defaults.</summary>
        public bool IsDefaultActive { get; init; }

        // ---- app.ShortcutSaveData.ShortcutSettingItemSaveData (updated live by the game) ----

        /// <summary>Initial or saved gamepad binding; updated with the user's live binding in <c>onUnregister</c>.</summary>
        public via.hid.GamePadButton Button { get; init; }

        /// <summary>Initial or saved keyboard binding; updated with the user's live key in <c>onUnregister</c>.</summary>
        public via.hid.KeyboardKey Key { get; init; }

        /// <summary>Initial or saved Function-modifier toggle; updated with the live value in <c>onUnregister</c>.</summary>
        public bool IsFunc { get; init; }

        /// <summary>Initial or saved enabled state; updated with the live value in <c>onUnregister</c>.</summary>
        public bool IsActive { get; init; }

        // Getter-only on the game side: never written, only filled when capturing the live data

        /// <summary>Derived by SF6 during capture; never written on inject, reflects visibility when passed to <c>onUnregister</c>.</summary>
        public bool IsView { get; init; }

        /// <summary>Derived by SF6 during capture; reflects whether a gamepad button binding is assigned.</summary>
        public bool IsButtonType { get; init; }

        /// <summary>Derived by SF6 during capture; reflects whether a keyboard binding is assigned.</summary>
        public bool IsKeyType { get; init; }
    }

    private sealed class ShortcutRegistration(
        Guid id,
        CustomShortcutNode node,
        Action? onTrigger,
        Action? onUpdate,
        Action? onRelease,
        Action<CustomShortcutNode>? onUnregister)
    {
        public Guid Id { get; } = id;
        public CustomShortcutNode Node { get; private set; } = node;
        public Action? OnTrigger { get; } = onTrigger;
        public Action? OnUpdate { get; } = onUpdate;
        public Action? OnRelease { get; } = onRelease;
        public Action<CustomShortcutNode>? OnUnregister { get; } = onUnregister;

        // Instances are reused across rebuilds and only released once unregistered
        private ManagedObject? SettingDataMo { get; set; }
        public ManagedObject? SaveDataMo { get; private set; }

        public app.ShortcutSettingData BuildSettingData(int shortcutType)
        {
            Node.ItemMessage?.EnsureRegistration();
            Node.GuideMessage?.EnsureRegistration();

            if (SettingDataMo == null)
            {
                SettingDataMo = app.ShortcutSettingData.REFType.CreateInstance(0);
                SettingDataMo.Globalize();
            }

            var data = SettingDataMo.As<app.ShortcutSettingData>();
            data.ShortCutType = (app.ShortcutSettingData.EShortcutType)shortcutType;
            data.DefaultButton = Node.DefaultButton;
            data.DefaultKey = Node.DefaultKey;
            data.IsDefaultFunc = Node.IsDefaultFunc;
            data.IsKeyInput = Node.IsKeyInput;
            data.IsDefaultActive = Node.IsDefaultActive;

            if (Node.ItemMessage != null) SettingDataMo.SetGuidField("ItemMessage", Node.ItemMessage.Id);
            if (Node.GuideMessage != null) SettingDataMo.SetGuidField("GuideMessage", Node.GuideMessage.Id);

            return data;
        }

        public app.ShortcutSaveData.ShortcutSettingItemSaveData BuildSaveData(int shortcutType)
        {
            if (SaveDataMo == null)
            {
                SaveDataMo = app.ShortcutSaveData.ShortcutSettingItemSaveData.REFType.CreateInstance(0);
                SaveDataMo.Globalize();
            }

            var data = SaveDataMo.As<app.ShortcutSaveData.ShortcutSettingItemSaveData>();
            data.ShortCutType = (app.ShortcutSettingData.EShortcutType)shortcutType;
            data.Button = Node.Button;
            data.Key = Node.Key;
            data.IsFunc = Node.IsFunc;
            data.IsActive = Node.IsActive;
            // IsView / IsButtonType / IsKeyType are getter-only (derived by the game), they are only captured

            return data;
        }

        /// <summary>
        ///     Copies the live save data values (modified by the game) back into <see cref="Node" />.
        /// </summary>
        public void CaptureLiveSaveData()
        {
            if (SaveDataMo == null) return;

            var data = SaveDataMo.As<app.ShortcutSaveData.ShortcutSettingItemSaveData>();
            Node = Node with
            {
                Button = data.Button,
                Key = data.Key,
                IsFunc = data.IsFunc,
                IsActive = data.IsActive,
                IsView = data.IsView,
                IsButtonType = data.IsButtonType,
                IsKeyType = data.IsKeyType
            };
        }

        public void Release()
        {
            Node.ItemMessage?.Dispose();
            Node.GuideMessage?.Dispose();

            SettingDataMo?.ReleaseIfGlobalized();
            SettingDataMo = null;
            SaveDataMo?.ReleaseIfGlobalized();
            SaveDataMo = null;
        }
    }

    #endregion
}