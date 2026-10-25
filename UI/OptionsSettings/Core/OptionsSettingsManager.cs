using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.OptionsSettings.Requests;

namespace SF6_Plugin_Core.UI.OptionsSettings.Core;

/// <summary>
///     Controls dynamic tab creation, layout restructuring, element elimination, and injection for the Options menu.
/// </summary>
public static class OptionsSettingsManager
{
    private static readonly ConcurrentDictionary<IUIModificationRequest, List<int>>
        ModificationRequestPaths = [];

    private static readonly UICachedDataNode RootNode = new(null);
    private static readonly Lock ModificationLock = new();

    // Tracks custom tabs added at the root level so they can be cleanly removed and reset on rebuild
    private static readonly List<(app.Option.TabType TabKey, ManagedObject ListMo)> AddedTabs = [];

    private static REFrameworkNET.Collections.IDictionary<app.Option.TabType,
        REFrameworkNET.Collections.IList<app.Option.OptionUnitBase>> GetUnitLists()
    {
        var optionManager = GameSingletonManager.OptionManager ?? API.GetManagedSingletonT<app.OptionManager>()
            ?? throw new InvalidOperationException("OptionManager is not initialized.");

        return optionManager.UnitLists
               ?? throw new InvalidOperationException("OptionManager.UnitLists is null.");
    }

    private static void RemoveAddedTabs(
        REFrameworkNET.Collections.IDictionary<app.Option.TabType,
            REFrameworkNET.Collections.IList<app.Option.OptionUnitBase>> unitLists)
    {
        foreach (var (tabKey, listMo) in AddedTabs)
        {
            unitLists.Remove(tabKey);
            listMo.ReleaseIfGlobalized();
        }

        AddedTabs.Clear();
    }

    private static ManagedObject CreateCachedUnitArray(
        REFrameworkNET.Collections.IList<app.Option.OptionUnitBase> sourceList)
    {
        var arrMo = app.Option.OptionUnitBase.REFType.CreateManagedArray((uint)sourceList.Count);
        arrMo.Globalize();
        var arr = arrMo.As<app.Option.OptionUnitBase_Array1D>()!;
        for (var i = 0; i < sourceList.Count; i++)
            arr[i] = sourceList[i];

        return arrMo;
    }

    private static app.Option.OptionUnitBase GetUnitFromNodeOrList(
        UICachedDataNode node,
        REFrameworkNET.Collections.IList<app.Option.OptionUnitBase> fallbackList,
        int index)
    {
        if (node.CachedElement != null)
        {
            var cachedArr = node.CachedElement.As<app.Option.OptionUnitBase_Array1D>()!;
            if (index < 0 || index >= cachedArr.Length)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Index {index} is out of bounds for cached list of length {cachedArr.Length}.");

            return cachedArr[index];
        }

        if (index < 0 || index >= fallbackList.Count)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Index {index} is out of bounds for list of length {fallbackList.Count}.");

        return fallbackList[index];
    }

    private static app.Option.OptionGroupUnit AsGroupUnit(app.Option.OptionUnitBase unit, int index)
    {
        if (unit.Setting?.DataType != app.Option.SettingDataType.Group)
            throw new InvalidOperationException(
                $"OptionUnitBase at index {index} is not an OptionGroupUnit and cannot have child units.");

        return (unit as IObject)?.As<app.Option.OptionGroupUnit>()
               ?? throw new InvalidOperationException(
                   $"Failed to cast OptionUnitBase at index {index} to OptionGroupUnit.");
    }

    /// <summary>
    ///     Retrieves the original unmodified <see cref="app.Option.OptionUnitBase" /> at the specified path
    ///     (where <c>elementPath[0]</c> is the <see cref="app.Option.TabType" /> and subsequent indices traverse into the
    ///     tab's elements).
    /// </summary>
    public static app.Option.OptionUnitBase GetOriginalElement(List<int> elementPath)
    {
        if (elementPath.Count < 2)
            throw new ArgumentException(
                "Element path must contain at least a TabType index and an element index.",
                nameof(elementPath));

        lock (ModificationLock)
        {
            var unitLists = GetUnitLists();
            var tabType = (app.Option.TabType)elementPath[0];
            if (!unitLists.ContainsKey(tabType))
                throw new InvalidOperationException(
                    $"TabType {tabType} ({elementPath[0]}) does not exist in UnitLists.");

            var currentList = unitLists[tabType];
            var currentNode = RootNode[elementPath[0]];

            app.Option.OptionUnitBase? currentUnit = null;

            for (var i = 1; i < elementPath.Count; i++)
            {
                var elemIndex = elementPath[i];
                currentUnit = currentNode != null
                    ? GetUnitFromNodeOrList(currentNode, currentList, elemIndex)
                    : currentList[elemIndex];

                if (i < elementPath.Count - 1)
                {
                    var groupUnit = AsGroupUnit(currentUnit, elemIndex);
                    currentList = groupUnit.ChildUnits;
                    currentNode = currentNode?[elemIndex];
                }
            }

            return currentUnit!;
        }
    }

    private static void RebuildUI()
    {
        if (!ModificationLock.IsHeldByCurrentThread)
            throw new SynchronizationLockException(
                "RebuildUI must be called within a ModifyUI call.");

        OptionTypeRegistry.Clear();

        var unitLists = GetUnitLists();

        Stack<(
            UICachedDataNode Node,
            REFrameworkNET.Collections.IList<app.Option.OptionUnitBase>? TargetList,
            int ChildIterator
            )> nodeStack = new();

        nodeStack.Push((RootNode, null, 0));

        while (nodeStack.Count > 0)
        {
            var (currentNode, targetList, childIterator) = nodeStack.Pop();

            // ------------------------------------------
            // PRE-ORDER / TRAVERSAL PHASE
            // ------------------------------------------
            if (childIterator == 0)
            {
                if (currentNode == RootNode)
                {
                    RemoveAddedTabs(unitLists);
                }
                else if (currentNode.CachedElement != null && targetList != null)
                {
                    var cachedArr = currentNode.CachedElement.As<app.Option.OptionUnitBase_Array1D>()!;
                    targetList.Clear();
                    for (var i = 0; i < cachedArr.Length; i++)
                        targetList.Add(cachedArr[i]);
                }
            }

            if (childIterator < currentNode.Children.Count)
            {
                nodeStack.Push((currentNode, targetList, childIterator + 1));

                var (childIndex, childNode) = currentNode.Children[childIterator];

                if (currentNode == RootNode)
                {
                    var tabType = (app.Option.TabType)childIndex;
                    var tabList = unitLists[tabType];
                    nodeStack.Push((childNode, tabList, 0));
                }
                else
                {
                    var childUnit = targetList![childIndex];
                    var childGroup = AsGroupUnit(childUnit, childIndex);
                    nodeStack.Push((childNode, childGroup.ChildUnits, 0));
                }
            }
            // ------------------------------------------
            // POST-ORDER / PROCESSING PHASE
            // ------------------------------------------
            else
            {
                if (currentNode == RootNode)
                {
                    if (currentNode.NewTabs.Count == 0) continue;

                    var nextTabIndex = unitLists.Count + 1;

                    foreach (var newTabRequest in currentNode.NewTabs)
                    {
                        var newTabListMo = MemoryHelpers.CreateGenericList<app.Option.OptionUnitBase>()
                                           ?? throw new InvalidOperationException(
                                               "Failed to create List<app.Option.OptionUnitBase> for new custom tab.");

                        var newTabList =
                            newTabListMo.As<REFrameworkNET.Collections.IList<app.Option.OptionUnitBase>>()
                            ?? throw new InvalidOperationException(
                                "Failed to cast new tab list to REFrameworkNET.Collections.IList<app.Option.OptionUnitBase>.");

                        foreach (var newUnit in newTabRequest.BuildTabElements())
                            newTabList.Add(newUnit);

                        var newTabKey = (app.Option.TabType)nextTabIndex++;
                        unitLists[newTabKey] = newTabList;
                        AddedTabs.Add((newTabKey, newTabListMo));
                    }
                }
                else if (currentNode.CachedElement != null && targetList != null)
                {
                    var hasModifications = currentNode.ReorderRequest != null ||
                                           currentNode.Appends.Count > 0 ||
                                           currentNode.Removals.Count > 0;

                    if (!hasModifications)
                    {
                        currentNode.CachedElement.ReleaseIfGlobalized();
                        currentNode.CachedElement = null;
                        continue;
                    }

                    var originalChildArray = currentNode.CachedElement.As<app.Option.OptionUnitBase_Array1D>()!;
                    var originalChildLength = originalChildArray.Length;

                    HashSet<int> removedIndices = [];
                    foreach (var removeRequest in currentNode.Removals)
                    foreach (var idx in removeRequest.IndicesToRemove)
                    {
                        if (idx < 0 || idx >= originalChildLength)
                            throw new InvalidOperationException(
                                $"Remove index {idx} is out of bounds for list of length {originalChildLength}.");

                        removedIndices.Add(idx);
                    }

                    var allNewElements = new List<app.Option.OptionUnitBase>[originalChildLength + 2];
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

                    targetList.Clear();

                    foreach (var newElement in allNewElements[0])
                        targetList.Add(newElement);

                    for (var targetIndex = 0; targetIndex < originalChildLength; targetIndex++)
                    {
                        var originalIndex = currentNode.ReorderRequest?.GetMapping(targetIndex) ?? targetIndex;
                        if (!removedIndices.Contains(originalIndex))
                            targetList.Add(originalChildArray[originalIndex]);

                        foreach (var newElement in allNewElements[originalIndex + 1])
                            targetList.Add(newElement);
                    }

                    foreach (var newElement in allNewElements[originalChildLength + 1])
                        targetList.Add(newElement);
                }
            }
        }
    }

    /// <summary>
    ///     Registers a request to append a new tab at the root level of <c>OptionManager.UnitLists</c>.
    /// </summary>
    /// TODO: make this public again once the tab stuff is possible (if ever)
    internal static void RegisterModification(AppendNewTabRequest newTabRequest)
    {
        if (!ModificationLock.IsHeldByCurrentThread)
            throw new InvalidOperationException("RegisterModification must be called inside a ModifyUI call.");

        if (RootNode.RequestNewTab(newTabRequest))
        {
            ModificationRequestPaths[newTabRequest] = [];
            newTabRequest.Claim();
        }
        else
        {
            API.LogWarning("Failed to register AppendNewTabRequest.");
        }
    }

    /// <summary>
    ///     Registers a modification request at the specified non-empty traversal path
    ///     (where <c>traversalPath[0]</c> is the <see cref="app.Option.TabType" />).
    /// </summary>
    public static void RegisterModification(List<int> traversalPath, IUIModificationRequest modificationRequest)
    {
        if (!ModificationLock.IsHeldByCurrentThread)
            throw new InvalidOperationException("RegisterModification must be called inside a ModifyUI call.");

        if (modificationRequest is AppendNewTabRequest)
            throw new InvalidOperationException(
                $"'{nameof(AppendNewTabRequest)}' does not take a traversal path. Use RegisterModification({nameof(AppendNewTabRequest)}) instead.");

        if (traversalPath.Count == 0)
            throw new ArgumentException(
                $"Traversal path cannot be empty. Only '{nameof(AppendNewTabRequest)}' operates at the root level via RegisterModification({nameof(AppendNewTabRequest)}).",
                nameof(traversalPath));

        var currentNode = RootNode;
        var unitLists = GetUnitLists();
        REFrameworkNET.Collections.IList<app.Option.OptionUnitBase>? targetList = null;

        for (var i = 0; i < traversalPath.Count; i++)
        {
            var index = traversalPath[i];

            if (i == 0)
            {
                var tabType = (app.Option.TabType)index;
                if (!unitLists.ContainsKey(tabType))
                    throw new InvalidOperationException($"TabType {tabType} ({index}) does not exist in UnitLists.");

                targetList = unitLists[tabType];
                var tabNode = currentNode[index];
                if (tabNode == null)
                {
                    tabNode = new UICachedDataNode(null);
                    currentNode[index] = tabNode;
                }

                currentNode = tabNode;
            }
            else
            {
                if (targetList == null)
                    throw new InvalidOperationException("Target list is null during path traversal.");

                var unit = GetUnitFromNodeOrList(currentNode, targetList, index);
                var groupUnit = AsGroupUnit(unit, index);
                targetList = groupUnit.ChildUnits;

                var childNode = currentNode[index];
                if (childNode == null)
                {
                    childNode = new UICachedDataNode(null);
                    currentNode[index] = childNode;
                }

                currentNode = childNode;
            }
        }

        if (currentNode.CachedElement == null)
        {
            if (targetList == null)
                throw new InvalidOperationException("Target list resolved to null.");

            currentNode.CachedElement = CreateCachedUnitArray(targetList);
        }

        var success = modificationRequest switch
        {
            OptionsAppendElementsRequest appendRequest => currentNode.RequestAppend(appendRequest),
            ReorderRequest reorderRequest => currentNode.RequestReorder(reorderRequest),
            RemoveElementsRequest removeRequest => currentNode.RequestRemove(removeRequest),
            _ => throw new InvalidOperationException(
                $"Unknown or incompatible modification request type '{modificationRequest.GetType().Name}' at path [{string.Join(", ", traversalPath)}].")
        };

        if (success)
        {
            ModificationRequestPaths[modificationRequest] = [.. traversalPath];
            if (modificationRequest is ExclusiveModificationRequest singleUseRequest) singleUseRequest.Claim();
        }
        else
        {
            API.LogWarning(
                $"Modification request of type {modificationRequest.GetType().Name} conflicts with an existing request at path [{string.Join(", ", traversalPath)}].");
        }
    }

    public static void UnregisterModification(IUIModificationRequest modificationRequest)
    {
        if (!ModificationLock.IsHeldByCurrentThread)
            throw new InvalidOperationException("UnregisterModification must be called inside a ModifyUI call.");

        if (!ModificationRequestPaths.TryRemove(modificationRequest, out var traversalPath)) return;
        if (modificationRequest is ExclusiveModificationRequest singleUseRequest) singleUseRequest.ReleaseClaim();

        var currentNode = RootNode;
        for (var i = 0; i < traversalPath.Count; i++)
        {
            var index = traversalPath[i];
            if (currentNode[index] == null)
            {
                API.LogWarning($"Cached node at index {index} is null. Cannot traverse further.");
                return;
            }

            currentNode = currentNode[index]!;
        }

        switch (modificationRequest)
        {
            case AppendNewTabRequest newTabRequest:
                currentNode.ClearNewTabRequest(newTabRequest);
                break;
            case ReorderRequest:
                currentNode.ClearReorderRequest();
                break;
            case OptionsAppendElementsRequest appendRequest:
                currentNode.ClearAppendRequest(appendRequest);
                break;
            case RemoveElementsRequest removeRequest:
                currentNode.ClearRemoveRequest(removeRequest);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown modification request type: {modificationRequest.GetType().Name}");
        }
    }

    public static void ModifyUI(Action modificationAction)
    {
        lock (ModificationLock)
        {
            modificationAction.Invoke();
            RebuildUI();
        }
    }

    internal static void ClearOptionsSettingsHooks()
    {
        lock (ModificationLock)
        {
            if (GameSingletonManager.OptionManager != null)
            {
                RemoveAddedTabs(GetUnitLists());
            }
            else
            {
                foreach (var (_, listMo) in AddedTabs) listMo.ReleaseIfGlobalized();
                AddedTabs.Clear();
            }

            foreach (var request in ModificationRequestPaths.Keys)
                if (request is ExclusiveModificationRequest singleUseRequest)
                    singleUseRequest.ReleaseClaim();

            ModificationRequestPaths.Clear();
            RootNode.Clear();
            OptionTypeRegistry.Clear();
        }
    }

    private class UICachedDataNode(ManagedObject? cachedElement)
    {
        private static readonly Comparer<(int index, UICachedDataNode node)> Comparer =
            Comparer<(int index, UICachedDataNode node)>.Create((a, b) => a.index.CompareTo(b.index));

        public ManagedObject? CachedElement { get; set; } = cachedElement;

        private List<(int, UICachedDataNode)> ChildNodes { get; } = [];
        public System.Collections.Generic.IReadOnlyList<(int Index, UICachedDataNode Node)> Children => ChildNodes;

        private List<AppendNewTabRequest> NewTabRequests { get; } = [];
        public System.Collections.Generic.IReadOnlyList<AppendNewTabRequest> NewTabs => NewTabRequests;

        public ReorderRequest? ReorderRequest { get; private set; }

        private List<OptionsAppendElementsRequest> AppendRequests { get; } = [];
        public System.Collections.Generic.IReadOnlyList<OptionsAppendElementsRequest> Appends => AppendRequests;

        private List<RemoveElementsRequest> RemoveRequests { get; } = [];
        public System.Collections.Generic.IReadOnlyList<RemoveElementsRequest> Removals => RemoveRequests;

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

        public bool RequestNewTab(AppendNewTabRequest newTabRequest)
        {
            NewTabRequests.Add(newTabRequest);
            return true;
        }

        public void ClearNewTabRequest(AppendNewTabRequest newTabRequest)
        {
            newTabRequest.Reset();
            NewTabRequests.Remove(newTabRequest);
        }

        public bool RequestReorder(ReorderRequest reorderRequest)
        {
            if (ReorderRequest != null) return false;
            ReorderRequest = reorderRequest;
            return true;
        }

        public void ClearReorderRequest()
        {
            ReorderRequest = null;
        }

        public bool RequestAppend(OptionsAppendElementsRequest appendRequest)
        {
            AppendRequests.Add(appendRequest);
            return true;
        }

        public void ClearAppendRequest(OptionsAppendElementsRequest appendRequest)
        {
            appendRequest.Reset();
            AppendRequests.Remove(appendRequest);
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

        public void Clear()
        {
            foreach (var (_, childNode) in ChildNodes) childNode.Clear();
            ChildNodes.Clear();

            foreach (var newTabRequest in NewTabRequests.ToList()) ClearNewTabRequest(newTabRequest);
            ClearReorderRequest();
            foreach (var appendRequest in AppendRequests.ToList()) ClearAppendRequest(appendRequest);
            RemoveRequests.Clear();

            CachedElement?.ReleaseIfGlobalized();
            CachedElement = null;
        }
    }
}