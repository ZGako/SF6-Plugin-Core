// Core usings

using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

/// <summary>
///     Controls dynamic layout restructuring, element elimination, injection, and hooks for the training mode pause menu.
/// </summary>
public static class PauseMenuManager
{
    private static readonly Dictionary<IUIModificationRequest, List<int>>
        ModificationRequestPaths = []; // maps modification requests to their traversal paths in the cached data tree

    private static readonly UICachedDataNode RootNode = new(null); // root node of the cached data tree
    private static readonly Lock ModificationLock = new();
    private static readonly List<Action> PendingDisposals = [];
    private static volatile bool _isDirty;

    private static readonly Lazy<Field?> TrainingManagerUIDataFieldLazy =
        new(() => app.training.TrainingManager.REFType?.GetField("_UIData"));

    private static readonly Lazy<Field?> UIDataMenuDataFieldLazy =
        new(() => (app.training.TrainingPauseMenuUserData.REFType
                   ?? API.GetTDB().FindType("app.training.TrainingPauseMenuUserData"))?.GetField("_MenuData"));

    private static readonly Lazy<Field?> TrainingMenuDataChildDataFieldLazy =
        new(() => app.training.TrainingMenuData.REFType?.GetField("_ChildData"));

    private static readonly Lazy<Field?> MessageIdFieldLazy =
        new(() => app.training.TrainingMenuData.REFType?.GetField("_MessageID"));

    private static readonly Lazy<Field?> SubMessageIdFieldLazy =
        new(() => app.training.TrainingMenuData.REFType?.GetField("_SubMessageID"));

    private static readonly Lazy<Field?> GuideMessageFieldLazy =
        new(() => app.training.TrainingMenuData.REFType?.GetField("_GuideMessage"));

    private static readonly Lazy<Field?> GuideMessageIdFieldLazy =
        new(() => app.training.TrainingMenuData.REFType?.GetField("_GuideMessageID"));

    private static readonly Lazy<Field?> EndWordsGuidFieldLazy =
        new(() => app.training.TrainingMenuData.REFType?.GetField("_EndWordsGuid"));

    private static Field? TrainingManagerUIDataField => TrainingManagerUIDataFieldLazy.Value;
    private static Field? UIDataMenuDataField => UIDataMenuDataFieldLazy.Value;
    private static Field? TrainingMenuDataChildDataField => TrainingMenuDataChildDataFieldLazy.Value;

    private static Field? MessageIdField => MessageIdFieldLazy.Value;
    private static Field? SubMessageIdField => SubMessageIdFieldLazy.Value;
    private static Field? GuideMessageField => GuideMessageFieldLazy.Value;
    private static Field? GuideMessageIdField => GuideMessageIdFieldLazy.Value;
    private static Field? EndWordsGuidField => EndWordsGuidFieldLazy.Value;

    private static Guid TryReadGuidField(ManagedObject mo, Field? field)
    {
        if (field == null) return Guid.Empty;
        var rawPtr = field.GetDataRaw(mo.GetAddress(), false);
        return rawPtr != 0 ? Marshal.PtrToStructure<Guid>((nint)rawPtr) : Guid.Empty;
    }

    private static bool IsValidManagedObject(ManagedObject? mo)
    {
        if (mo == null) return false;
        var addr = mo.GetAddress();
        return addr != 0 && ManagedObject.IsManagedObject(addr);
    }

    private static bool IsTrainingMenuData(ManagedObject? mo)
    {
        if (!IsValidManagedObject(mo)) return false;
        var typeDef = mo!.GetTypeDefinition();
        var menuDataType = app.training.TrainingMenuData.REFType;
        return typeDef != null && menuDataType != null && typeDef.IsDerivedFrom(menuDataType);
    }

    private static ManagedObject? TryGetChildDataArray(ManagedObject? elementMo)
    {
        if (elementMo == null) return null;

        if (!IsTrainingMenuData(elementMo))
        {
            var actualType = IsValidManagedObject(elementMo)
                ? elementMo.GetTypeDefinition()?.FullName ?? "unknown"
                : "invalid/freed";
            API.LogWarning(
                $"[PauseMenuManager] Expected TrainingMenuData before reading _ChildData at 0x{elementMo.GetAddress():X}, got '{actualType}'.");
            return null;
        }

        var childDataMo = TrainingMenuDataChildDataField?.GetDataBoxed(elementMo.GetAddress(), false) as ManagedObject;
        return IsValidManagedObject(childDataMo) ? childDataMo : null;
    }

    private static ManagedObject? TryGetMenuDataElement(ManagedObject? arrayMo, int index)
    {
        if (!IsValidManagedObject(arrayMo)) return null;

        if (arrayMo!.GetTypeDefinition()?.GetVMObjType() != VMObjType.Array)
        {
            API.LogWarning(
                $"[PauseMenuManager] Expected array at 0x{arrayMo.GetAddress():X}, got '{arrayMo.GetTypeDefinition()?.FullName ?? "unknown"}'.");
            return null;
        }

        var arraySys = arrayMo.As<_System.Array>();
        if (arraySys == null) return null;

        var length = arraySys.Length;
        if (index < 0 || index >= length)
        {
            API.LogWarning(
                $"[PauseMenuManager] Menu data index {index} is out of bounds for array of length {length}.");
            return null;
        }

        var elem = arraySys.GetValue(index) as ManagedObject;
        if (elem == null) return null;

        if (!IsTrainingMenuData(elem))
        {
            var actualType = IsValidManagedObject(elem)
                ? elem.GetTypeDefinition()?.FullName ?? "unknown"
                : "invalid/freed";
            API.LogWarning(
                $"[PauseMenuManager] Element at index {index} (0x{elem.GetAddress():X}) is '{actualType}', expected TrainingMenuData.");
            return null;
        }

        return elem;
    }

    private static ManagedObject? TryGetUIDataMo()
    {
        if (GameSingletonManager.TrainingManager == null) return null;

        var trainingManagerMo = API.GetManagedSingleton("app.training.TrainingManager");
        if (!IsValidManagedObject(trainingManagerMo)) return null;

        var uiDataMo =
            TrainingManagerUIDataField?.GetDataBoxed(trainingManagerMo!.GetAddress(), false) as ManagedObject;
        if (!IsValidManagedObject(uiDataMo)) return null;

        var menuDataMo = UIDataMenuDataField?.GetDataBoxed(uiDataMo!.GetAddress(), false) as ManagedObject;
        return IsValidManagedObject(menuDataMo) ? menuDataMo : null;
    }

    private static ManagedObject? GetUIDataMo()
    {
        var trainingManagerMo = API.GetManagedSingleton("app.training.TrainingManager");
        if (trainingManagerMo == null)
            throw new InvalidOperationException("TrainingManager is not initialized.");

        if (!IsValidManagedObject(trainingManagerMo))
            throw new InvalidOperationException("TrainingManager ManagedObject is invalid.");

        var uiDataMo = TrainingManagerUIDataField?.GetDataBoxed(trainingManagerMo.GetAddress(), false) as ManagedObject;
        if (!IsValidManagedObject(uiDataMo))
            return null;

        var menuDataMo = UIDataMenuDataField?.GetDataBoxed(uiDataMo!.GetAddress(), false) as ManagedObject;
        return IsValidManagedObject(menuDataMo) ? menuDataMo : null;
    }

    private static void SetUIDataMo(ManagedObject menuDataArrayMo)
    {
        var trainingManagerMo = API.GetManagedSingleton("app.training.TrainingManager")
                                ?? throw new InvalidOperationException("TrainingManager is not initialized.");
        if (!IsValidManagedObject(trainingManagerMo))
            throw new InvalidOperationException("TrainingManager ManagedObject is invalid.");

        var uiDataMo = TrainingManagerUIDataField?.GetDataBoxed(trainingManagerMo.GetAddress(), false) as ManagedObject;
        if (!IsValidManagedObject(uiDataMo))
            throw new InvalidOperationException("_UIData is null or invalid.");

        var menuDataField = UIDataMenuDataField
                            ?? throw new InvalidOperationException("_MenuData field not found on _UIData.");

        var rawPtr = menuDataField.GetDataRaw(uiDataMo!.GetAddress(), false);
        if (rawPtr != 0 && Marshal.ReadIntPtr((IntPtr)rawPtr) == (IntPtr)menuDataArrayMo.GetAddress())
            return;

        menuDataField.SetDataBoxed(uiDataMo.GetAddress(), menuDataArrayMo, false);
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
                API.LogError($"[PauseMenuManager] Failed during deferred disposal: {ex}");
            }
    }

    /// <summary>
    ///     Retrieves the original unmodified <see cref="app.training.TrainingMenuData" /> at the specified traversal path.
    /// </summary>
    public static app.training.TrainingMenuData? GetOriginalElement(List<int> traversalPath)
    {
        lock (ModificationLock)
        {
            var currentArrayMo = RootNode.OriginalMenuDataMo ?? TryGetUIDataMo();
            if (!IsValidManagedObject(currentArrayMo))
                return null;

            var currentNode = RootNode;
            ManagedObject? currentElementMo = null;

            foreach (var index in traversalPath)
            {
                var childNode = currentNode?[index];
                currentElementMo = TryGetMenuDataElement(currentArrayMo, index);
                if (!IsValidManagedObject(currentElementMo))
                    return null;

                currentArrayMo = childNode?.Snapshot?.OriginalChildDataMo ?? TryGetChildDataArray(currentElementMo);
                currentNode = childNode;
            }

            return currentElementMo?.As<app.training.TrainingMenuData>();
        }
    }

    /// <summary>
    ///     Retrieves the original unmodified <c>_ChildData</c> array for the element at the specified traversal path
    ///     without cloning or mutating native <see cref="app.training.TrainingMenuData" /> instances.
    /// </summary>
    public static app.training.TrainingMenuData_Array1D? GetOriginalChildData(List<int> traversalPath)
    {
        lock (ModificationLock)
        {
            var currentArrayMo = RootNode.OriginalMenuDataMo ?? TryGetUIDataMo();
            if (!IsValidManagedObject(currentArrayMo))
                return null;

            var currentNode = RootNode;

            foreach (var index in traversalPath)
            {
                var childNode = currentNode?[index];
                if (childNode?.Snapshot?.OriginalChildDataMo != null)
                {
                    currentArrayMo = childNode.Snapshot.OriginalChildDataMo;
                }
                else
                {
                    var currentElementMo = TryGetMenuDataElement(currentArrayMo, index);
                    if (!IsValidManagedObject(currentElementMo))
                        return null;

                    currentArrayMo = TryGetChildDataArray(currentElementMo);
                }

                currentNode = childNode;
            }

            return IsValidManagedObject(currentArrayMo)
                ? currentArrayMo!.As<app.training.TrainingMenuData_Array1D>()
                : null;
        }
    }

    [Callback(typeof(LateUpdateBehavior), CallbackType.Post)]
    public static void OnLateUpdateBehavior()
    {
        if (!_isDirty) return;

        lock (ModificationLock)
        {
            if (!_isDirty) return;
            _isDirty = false;

            if (TryGetUIDataMo() != null)
            {
                try
                {
                    RebuildUI();
                }
                catch (Exception ex)
                {
                    API.LogError($"[PauseMenuManager] RebuildUI failed: {ex}");
                }
            }
            else
            {
                foreach (var request in ModificationRequestPaths.Keys)
                    if (request is ExclusiveModificationRequest singleUseRequest)
                        singleUseRequest.ReleaseClaim();

                ModificationRequestPaths.Clear();
                RootNode.Clear(PendingDisposals);
                FlushPendingDisposals();
                FunctionTypeRegistry.Clear();
            }
        }
    }

    private static void RebuildUI()
    {
        if (!ModificationLock.IsHeldByCurrentThread)
            throw new SynchronizationLockException(
                "RebuildUI must be called while holding ModificationLock.");

        // Clear the function type registry (the functionTypes we want them to be incremental values, so we clear them to refill them properly)
        // Messages are instead cleared by the ModificationRequests themselves, since they don't need to be refilled from scratch and thus rebuild each time the UI is rebuilt
        FunctionTypeRegistry.Clear();

        // traverse the cached data tree as a post-order DFS
        // during the DFS, we want to keep the gameUIData half an iteration behind on each call to be able to "restore" the original element's data before applying the modification requests
        Stack<(
            UICachedDataNode Node,
            ManagedObject? CurrentElement,
            ManagedObject? ChildArray,
            int ChildIterator
            )> nodeStack = new();

        // Seed the stack. RootNode has no specific element, just the top-level MenuData array.
        nodeStack.Push((RootNode, null, GetUIDataMo(), 0));

        while (nodeStack.Count > 0)
        {
            var (currentNode, currentElementMo, childArrayMo, childIterator) = nodeStack.Pop();

            // ------------------------------------------
            // PRE-ORDER / TRAVERSAL PHASE
            // ------------------------------------------
            // Restore the original fields/children of the current element from the C# snapshot before applying modifications
            if (childIterator == 0)
            {
                if (currentNode == RootNode)
                {
                    if (currentNode.OriginalMenuDataMo != null)
                    {
                        if (currentNode.CustomChildArrayMo != null)
                        {
                            SetUIDataMo(currentNode.OriginalMenuDataMo);
                            currentNode.ReleaseCustomChildArray();
                        }

                        childArrayMo = currentNode.OriginalMenuDataMo;
                    }
                }
                else if (currentNode.Snapshot != null && currentElementMo != null)
                {
                    if (currentNode.HasAppliedFieldOverride)
                    {
                        currentNode.Snapshot.RestoreFields(currentElementMo);
                        currentNode.HasAppliedFieldOverride = false;
                    }

                    if (currentNode.CustomChildArrayMo != null)
                    {
                        currentNode.Snapshot.RestoreChildData(currentElementMo);
                        currentNode.ReleaseCustomChildArray();
                    }

                    childArrayMo = currentNode.Snapshot.OriginalChildDataMo ?? TryGetChildDataArray(currentElementMo);
                }
            }

            if (childIterator < currentNode.Children.Count)
            {
                // Push CURRENT node back, incrementing its iterator
                nodeStack.Push((currentNode, currentElementMo, childArrayMo, childIterator + 1));

                // Get next child
                var childPair = currentNode.Children[childIterator];
                var childIndex = childPair.Index;
                var childNode = childPair.Node;

                // Get the child element MO from the array safely
                var childElementMo = TryGetMenuDataElement(childArrayMo, childIndex);
                if (childElementMo == null)
                {
                    if (childNode.HasModifications || childNode.Children.Count > 0)
                        API.LogWarning(
                            $"[PauseMenuManager] Skipping missing or invalid child element at index {childIndex} during UI rebuild.");
                    continue;
                }

                // Get the child Game Array MO (prefer snapshot original if present)
                var childGameArrayMo = childNode.Snapshot?.OriginalChildDataMo ?? TryGetChildDataArray(childElementMo);

                // Push CHILD node onto the stack
                nodeStack.Push((childNode, childElementMo, childGameArrayMo, 0));
            }
            // ------------------------------------------
            // POST-ORDER / PROCESSING PHASE
            // ------------------------------------------
            else
            {
                currentNode.PruneEmptyChildren();

                if (currentNode.HasSnapshot && (currentNode == RootNode || currentElementMo != null))
                {
                    if (!currentNode.HasModifications)
                    {
                        currentNode.ClearSnapshot();
                        continue;
                    }

                    var currentElement = currentElementMo?.As<app.training.TrainingMenuData>();

                    if (currentNode.FieldOverrideRequest != null && currentElementMo != null)
                    {
                        currentNode.FieldOverrideRequest.ApplyOverrides(currentElementMo);
                        currentNode.HasAppliedFieldOverride = true;
                    }

                    if (currentNode.Hooks.Count > 0 && currentElement != null)
                    {
                        var functionId = FunctionTypeRegistry.RegisterGameFunctionType(currentElement.FuncType);
                        foreach (var hookRequest in currentNode.Hooks) hookRequest.AddHook(functionId);
                    }

                    if (currentNode.ReorderRequest != null ||
                        currentNode.Appends.Count > 0 ||
                        currentNode.Removals.Count > 0)
                    {
                        // Use childArrayMo so this works identically for both RootNode (_MenuData) and child nodes (_ChildData)
                        var originalChildArray = childArrayMo?.As<app.training.TrainingMenuData_Array1D>();
                        var originalChildLength = originalChildArray?.Length ?? 0;

                        HashSet<int> removedIndices = [];
                        foreach (var removeRequest in currentNode.Removals)
                        foreach (var idx in removeRequest.IndicesToRemove)
                        {
                            if (idx < 0 || idx >= originalChildLength)
                                throw new InvalidOperationException(
                                    $"Remove index {idx} is out of bounds for array of length {originalChildLength}.");

                            removedIndices.Add(idx);
                        }

                        var allNewElements = new List<app.training.TrainingMenuData>[originalChildLength + 2];
                        for (var i = 0; i < allNewElements.Length; i++) allNewElements[i] = [];

                        foreach (var appendRequest in currentNode.Appends)
                        {
                            var newElements = appendRequest.GetAppendList(originalChildLength);
                            for (var i = 0; i < newElements.Length; i++)
                            {
                                var elementsForIndex = newElements[i];
                                if (elementsForIndex != null && i < allNewElements.Length)
                                    allNewElements[i].AddRange(elementsForIndex);
                            }
                        }

                        var newArrMo =
                            app.training.TrainingMenuData.REFType.CreateManagedArray(
                                (uint)(originalChildLength - removedIndices.Count +
                                       allNewElements.Sum(list => list.Count)));
                        newArrMo.Globalize();
                        var newArr = newArrMo.As<app.training.TrainingMenuData_Array1D>();

                        var newIndex = 0;
                        foreach (var newElement in allNewElements[0]) newArr[newIndex++] = newElement;

                        for (var targetIndex = 0; targetIndex < originalChildLength; targetIndex++)
                        {
                            var originalIndex = currentNode.ReorderRequest?.GetMapping(targetIndex) ?? targetIndex;
                            if (!removedIndices.Contains(originalIndex))
                                newArr[newIndex++] = originalChildArray![originalIndex];
                            foreach (var newElement in allNewElements[originalIndex + 1])
                                newArr[newIndex++] = newElement;
                        }

                        foreach (var newElement in allNewElements[originalChildLength + 1])
                            newArr[newIndex++] = newElement;

                        // Assign back to _UIData._MenuData for RootNode, or _ChildData for regular elements
                        if (currentNode == RootNode)
                            SetUIDataMo(newArrMo);
                        else
                            currentElement!.ChildData = newArr;

                        currentNode.CustomChildArrayMo = newArrMo;
                    }
                }
            }
        }

        FlushPendingDisposals();
    }

    public static void RegisterModification(List<int> traversalPath, IUIModificationRequest modificationRequest)
    {
        lock (ModificationLock)
        {
            var currentNode = RootNode;
            // If RootNode was already modified, start traversal from the cached original _MenuData array
            var gameUIDataMo = RootNode.OriginalMenuDataMo ?? GetUIDataMo();
            ManagedObject? targetElementMo = null;

            // Traverse down to the target node (runs 0 times if traversalPath is empty)
            foreach (var index in traversalPath)
            {
                if (!IsValidManagedObject(gameUIDataMo))
                    throw new InvalidCastException("UIData is not a valid array. Cannot traverse.");

                var childNode = currentNode[index];
                if (childNode == null)
                {
                    childNode = new UICachedDataNode(null);
                    currentNode[index] = childNode;
                }

                targetElementMo = TryGetMenuDataElement(gameUIDataMo, index)
                                  ?? throw new InvalidOperationException(
                                      $"Element at index {index} is null or not a valid TrainingMenuData ManagedObject. Cannot snapshot fields. " +
                                      $"Traversal path is {string.Join(", ", traversalPath)} and request {modificationRequest}.");

                gameUIDataMo = childNode.Snapshot?.OriginalChildDataMo ?? TryGetChildDataArray(targetElementMo);
                currentNode = childNode;
            }

            // Capture a pure C# snapshot of the original data without cloning native TrainingMenuData instances
            if (!currentNode.HasSnapshot)
            {
                if (currentNode == RootNode)
                {
                    var originalMenuDataMo = GetUIDataMo()
                                             ?? throw new InvalidOperationException("_MenuData is null.");
                    RootNode.OriginalMenuDataMo = originalMenuDataMo;
                }
                else
                {
                    currentNode.Snapshot = TrainingMenuDataSnapshot.Capture(targetElementMo!);
                }
            }

            // Register the request on currentNode (which is RootNode when traversalPath is empty)
            var success = modificationRequest switch
            {
                ReorderRequest reorderRequest => currentNode.RequestReorder(reorderRequest),
                PauseMenuAppendElementsRequest appendRequest => currentNode.RequestAppend(appendRequest),
                RemoveElementsRequest removeRequest => currentNode.RequestRemove(removeRequest),
                FieldOverrideRequest overrideRequest when currentNode != RootNode => currentNode.RequestFieldOverride(
                    overrideRequest),
                IUIHookRequest hookRequest when currentNode != RootNode => currentNode.RequestHook(hookRequest),
                _ => throw new InvalidOperationException(
                    $"Unknown or invalid modification request type: {modificationRequest.GetType().Name}")
            };

            if (success)
            {
                ModificationRequestPaths[modificationRequest] = [.. traversalPath];
                if (modificationRequest is ExclusiveModificationRequest singleUseRequest) singleUseRequest.Claim();
                _isDirty = true;
            }
            else
            {
                API.LogWarning(
                    $"Modification request of type {modificationRequest.GetType().Name} conflicts with an existing request at path [{string.Join(", ", traversalPath)}].");
            }
        }
    }

    public static void UnregisterModification(IUIModificationRequest modificationRequest)
    {
        lock (ModificationLock)
        {
            if (UnregisterModificationCore(modificationRequest))
                _isDirty = true;
        }
    }

    private static bool UnregisterModificationCore(IUIModificationRequest modificationRequest)
    {
        if (!ModificationRequestPaths.Remove(modificationRequest, out var traversalPath)) return false;

        if (modificationRequest is ExclusiveModificationRequest singleUseRequest) singleUseRequest.ReleaseClaim();

        var currentNode = RootNode;
        for (var i = 0; i < traversalPath.Count; i++)
        {
            var index = traversalPath[i];
            if (currentNode[index] == null)
            {
                API.LogWarning($"Cached node at index {index} is null. Cannot traverse further.");
                return true;
            }

            currentNode = currentNode[index]!;
        }

        switch (modificationRequest)
        {
            case ReorderRequest:
                currentNode.ClearReorderRequest();
                break;
            case PauseMenuAppendElementsRequest appendRequest:
                currentNode.ClearAppendRequest(appendRequest, PendingDisposals);
                break;
            case RemoveElementsRequest removeRequest:
                currentNode.ClearRemoveRequest(removeRequest);
                break;
            case FieldOverrideRequest:
                currentNode.ClearFieldOverrideRequest(PendingDisposals);
                break;
            case IUIHookRequest hookRequest:
                currentNode.ClearDHookRequest(hookRequest);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown modification request type: {modificationRequest.GetType().Name}");
        }

        return true;
    }

    /// <summary>
    ///     Clears all registered custom function types and their associated delegates in the Training Pause Menu system.
    /// </summary>
    internal static void ClearTrainingPauseMenuHooks()
    {
        lock (ModificationLock)
        {
            foreach (var request in ModificationRequestPaths.Keys.ToList())
                if (UnregisterModificationCore(request))
                    _isDirty = true;

            if ((_isDirty || RootNode.HasSnapshot || RootNode.Children.Count > 0) && TryGetUIDataMo() != null)
                try
                {
                    RebuildUI();
                }
                catch (Exception ex)
                {
                    API.LogError($"[PauseMenuManager] RebuildUI during ClearTrainingPauseMenuHooks failed: {ex}");
                }

            _isDirty = false;
            ModificationRequestPaths.Clear();
            RootNode.Clear(PendingDisposals);
            FlushPendingDisposals();

            // Clear all registered custom function types and their associated delegates
            FunctionTypeRegistry.Clear();
            TrainingFunctionHooks.Clear();
            SpinBoxHook.Clear();
            SubMenuSpinnerHook.Clear();
            InputGuideHook.Clear();
            CustomInputGuideHook.Clear();
        }
    }

    private sealed class TrainingMenuDataSnapshot
    {
        private TrainingMenuDataSnapshot(ManagedObject? originalChildDataMo)
        {
            OriginalChildDataMo = originalChildDataMo;
        }

        public app.training.ItemType Type { get; init; }
        public bool IsEnabled { get; init; }
        public bool IsCantFocus { get; init; }
        public bool IsForceFocus { get; init; }
        public bool IsNotFocusFunc { get; init; }
        public app.InputAssign.Digital.Id NextButton { get; init; }
        public app.InputAssign.Digital.Id PrevButton { get; init; }
        public Guid MessageID { get; init; }
        public Guid SubMessageID { get; init; }
        public Guid GuideMessage { get; init; }
        public Guid GuideMessageID { get; init; }
        public app.InputAssign.Digital.Id GuidIcon { get; init; }
        public app.InputAssign.Digital.Id GuidAddIcon { get; init; }
        public int LimitValueX { get; init; }
        public int LimitValueY { get; init; }
        public int Interval { get; init; }
        public int IntervalOption { get; init; }
        public Guid EndWordsGuid { get; init; }
        public int SlotID { get; init; }
        public app.training.eVisibleCase VisibleCase { get; init; }
        public ManagedObject? OriginalChildDataMo { get; }

        public static TrainingMenuDataSnapshot Capture(ManagedObject elementMo)
        {
            var element = elementMo.As<app.training.TrainingMenuData>()
                          ?? throw new InvalidOperationException("Failed to cast ManagedObject to TrainingMenuData.");
            var limit = element.LimitValue;

            return new TrainingMenuDataSnapshot(TryGetChildDataArray(elementMo))
            {
                Type = element.Type,
                IsEnabled = element.IsEnabled,
                IsCantFocus = element.IsCantFocus,
                IsForceFocus = element.IsForceFocus,
                IsNotFocusFunc = element.IsNotFocusFunc,
                NextButton = element.NextButton,
                PrevButton = element.PrevButton,
                MessageID = TryReadGuidField(elementMo, MessageIdField),
                SubMessageID = TryReadGuidField(elementMo, SubMessageIdField),
                GuideMessage = TryReadGuidField(elementMo, GuideMessageField),
                GuideMessageID = TryReadGuidField(elementMo, GuideMessageIdField),
                GuidIcon = element.GuidIcon,
                GuidAddIcon = element.GuidAddIcon,
                LimitValueX = limit.x,
                LimitValueY = limit.y,
                Interval = element.Interval,
                IntervalOption = element._Interval_Option,
                EndWordsGuid = TryReadGuidField(elementMo, EndWordsGuidField),
                SlotID = element.SlotID,
                VisibleCase = element.VisibleCase
            };
        }

        public void RestoreFields(ManagedObject elementMo)
        {
            var element = elementMo.As<app.training.TrainingMenuData>();
            if (element == null) return;

            element.Type = Type;
            element.IsEnabled = IsEnabled;
            element.IsCantFocus = IsCantFocus;
            element.IsForceFocus = IsForceFocus;
            element.IsNotFocusFunc = IsNotFocusFunc;
            element.NextButton = NextButton;
            element.PrevButton = PrevButton;
            element.GuidIcon = GuidIcon;
            element.GuidAddIcon = GuidAddIcon;
            element.Interval = Interval;
            element._Interval_Option = IntervalOption;
            element.SlotID = SlotID;
            element.VisibleCase = VisibleCase;

            elementMo.SetGuidField("_MessageID", MessageID);
            elementMo.SetGuidField("_SubMessageID", SubMessageID);
            elementMo.SetGuidField("_GuideMessage", GuideMessage);
            elementMo.SetGuidField("_GuideMessageID", GuideMessageID);
            elementMo.SetGuidField("_EndWordsGuid", EndWordsGuid);

            var limit = via.Int2.REFType.CreateValueType().As<via.Int2>();
            limit.x = LimitValueX;
            limit.y = LimitValueY;
            element.LimitValue = limit;
        }

        public void RestoreChildData(ManagedObject elementMo)
        {
            var childDataField = TrainingMenuDataChildDataField;
            if (childDataField == null) return;

            var targetAddr = elementMo.GetAddress();
            var rawPtr = childDataField.GetDataRaw(targetAddr, false);
            var originalAddr = OriginalChildDataMo?.GetAddress() ?? 0;

            if (rawPtr != 0 && (ulong)Marshal.ReadIntPtr((IntPtr)rawPtr) == originalAddr)
                return;

            childDataField.SetDataBoxed(targetAddr, OriginalChildDataMo, false);
        }
    }

    private class UICachedDataNode(ManagedObject? cachedElement)
    {
        private static readonly Comparer<(int index, UICachedDataNode node)> Comparer =
            Comparer<(int index, UICachedDataNode node)>.Create((a, b) => a.index.CompareTo(b.index));

        public ManagedObject? OriginalMenuDataMo { get; set; } = cachedElement;
        public TrainingMenuDataSnapshot? Snapshot { get; set; }
        public bool HasAppliedFieldOverride { get; set; }
        public ManagedObject? CustomChildArrayMo { get; set; }
        public bool HasSnapshot => OriginalMenuDataMo != null || Snapshot != null;

        private List<(int, UICachedDataNode)> ChildNodes { get; } = []; // child nodes for the tree structure
        public System.Collections.Generic.IReadOnlyList<(int Index, UICachedDataNode Node)> Children => ChildNodes;
        public ReorderRequest? ReorderRequest { get; private set; } // optional reorder request
        public FieldOverrideRequest? FieldOverrideRequest { get; private set; } // optional field override request
        private List<PauseMenuAppendElementsRequest> AppendRequests { get; } = []; // list of append requests
        public System.Collections.Generic.IReadOnlyList<PauseMenuAppendElementsRequest> Appends => AppendRequests;
        private List<RemoveElementsRequest> RemoveRequests { get; } = []; // list of remove requests
        public System.Collections.Generic.IReadOnlyList<RemoveElementsRequest> Removals => RemoveRequests;
        private List<IUIHookRequest> HookRequests { get; } = []; // list of hook requests
        public System.Collections.Generic.IReadOnlyList<IUIHookRequest> Hooks => HookRequests;

        public bool HasModifications =>
            ReorderRequest != null ||
            FieldOverrideRequest != null ||
            AppendRequests.Count > 0 ||
            RemoveRequests.Count > 0 ||
            HookRequests.Count > 0;

        public UICachedDataNode? this[int index]
        {
            get => GetChild(index);
            set => SetChild(index, value!);
        }

        private UICachedDataNode? GetChild(int index)
        {
            var match = ChildNodes.BinarySearch((index, null!), Comparer);
            return match >= 0 ? ChildNodes[match].Item2 : null;
        }

        private void SetChild(int index, UICachedDataNode childNode)
        {
            var match = ChildNodes.BinarySearch((index, null!), Comparer);
            if (match >= 0)
                ChildNodes[match] = (index, childNode);
            else
                ChildNodes.Insert(~match, (index, childNode));
        }

        public void ClearSnapshot()
        {
            OriginalMenuDataMo = null;
            Snapshot = null;
            HasAppliedFieldOverride = false;
        }

        public void ReleaseCustomChildArray()
        {
            CustomChildArrayMo?.ReleaseIfGlobalized();
            CustomChildArrayMo = null;
        }

        public void PruneEmptyChildren()
        {
            for (var i = ChildNodes.Count - 1; i >= 0; i--)
            {
                var child = ChildNodes[i].Item2;
                if (!child.HasModifications &&
                    !child.HasSnapshot &&
                    child.CustomChildArrayMo == null &&
                    child.ChildNodes.Count == 0)
                    ChildNodes.RemoveAt(i);
            }
        }

        public bool RequestReorder(ReorderRequest reorderRequest)
        {
            if (ReorderRequest != null)
                // A reorder request already exists; cannot add another.
                return false;

            ReorderRequest = reorderRequest;
            return true;
        }

        public void ClearReorderRequest()
        {
            ReorderRequest = null;
        }

        public bool RequestFieldOverride(FieldOverrideRequest overrideRequest)
        {
            if (FieldOverrideRequest != null)
                // A field override request already exists; cannot add another.
                return false;

            FieldOverrideRequest = overrideRequest;
            return true;
        }

        public void ClearFieldOverrideRequest(List<Action> pendingDisposals)
        {
            if (FieldOverrideRequest != null)
            {
                var overrideReq = FieldOverrideRequest;
                pendingDisposals.Add(overrideReq.Reset);
                FieldOverrideRequest = null;
            }
        }

        public bool RequestAppend(PauseMenuAppendElementsRequest appendRequest)
        {
            AppendRequests.Add(appendRequest);
            return true;
        }

        public void ClearAppendRequest(PauseMenuAppendElementsRequest appendRequest, List<Action> pendingDisposals)
        {
            if (AppendRequests.Remove(appendRequest))
                pendingDisposals.Add(appendRequest.Reset);
        }

        public bool RequestRemove(RemoveElementsRequest removeRequest)
        {
            var hasOverlap = RemoveRequests.Any(existing =>
                existing.IndicesToRemove.Overlaps(removeRequest.IndicesToRemove));
            if (hasOverlap) return false;

            RemoveRequests.Add(removeRequest);
            return true;
        }

        public void ClearRemoveRequest(RemoveElementsRequest removeRequest)
        {
            RemoveRequests.Remove(removeRequest);
        }

        public bool RequestHook(IUIHookRequest hookRequest)
        {
            var incomingType = hookRequest.GetType();
            var hasSameHookClass = HookRequests.Any(r => r.GetType() == incomingType);

            if (hasSameHookClass)
                // A hook request of this specific concrete class already exists.
                return false;

            HookRequests.Add(hookRequest);
            return true;
        }

        public void ClearDHookRequest(IUIHookRequest hookRequest)
        {
            hookRequest.ClearHook(); // Clear the hook before removal
            HookRequests.Remove(hookRequest);
        }

        /// <summary>
        ///     Clears the cached element and all child nodes, as well as any registered modification requests (reorder, override,
        ///     append, remove, hook) for this node and its children.
        /// </summary>
        public void Clear(List<Action> pendingDisposals)
        {
            foreach (var (_, childNode) in ChildNodes) childNode.Clear(pendingDisposals);
            ChildNodes.Clear();

            ClearReorderRequest();
            ClearFieldOverrideRequest(pendingDisposals);
            foreach (var appendRequest in AppendRequests.ToList()) ClearAppendRequest(appendRequest, pendingDisposals);
            RemoveRequests.Clear();
            foreach (var hookRequest in HookRequests.ToList()) ClearDHookRequest(hookRequest);

            ReleaseCustomChildArray();
            ClearSnapshot();
        }
    }
}