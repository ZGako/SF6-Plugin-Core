// Core usings

using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

/// <summary>
///     Controls dynamic layout restructuring, element injection, and hooks for the training mode pause menu.
/// </summary>
public static class PauseMenuManager
{
    private static readonly ConcurrentDictionary<IUIModificationRequest, List<int>>
        ModificationRequestPaths = []; // maps modification requests to their traversal paths in the cached data tree

    private static readonly UICachedDataNode RootNode = new(null); // root node of the cached data tree

    private static readonly Lock ModificationLock = new();

    private static ManagedObject? GetUIDataMo()
    {
        var trainingManagerMo = API.GetManagedSingleton("app.training.TrainingManager");
        if (trainingManagerMo == null)
            throw new InvalidOperationException("TrainingManager is not initialized.");

        // Traverse the fields to get the ManagedObject of the array
        var uiDataMo = (trainingManagerMo as IObject)?.GetField("_UIData") as ManagedObject;
        return (uiDataMo as IObject)?.GetField("_MenuData") as ManagedObject;
    }

    private static void SetUIDataMo(ManagedObject menuDataArrayMo)
    {
        var trainingManagerMo = API.GetManagedSingleton("app.training.TrainingManager")
                                ?? throw new InvalidOperationException("TrainingManager is not initialized.");

        var uiDataMo = (trainingManagerMo as IObject)?.GetField("_UIData") as ManagedObject
                       ?? throw new InvalidOperationException("_UIData is null.");

        var menuDataField = uiDataMo.GetTypeDefinition()!.GetField("_MenuData")
                            ?? throw new InvalidOperationException("_MenuData field not found on _UIData.");

        menuDataField.SetDataBoxed(uiDataMo.GetAddress(), menuDataArrayMo, false);
    }

    private static void RebuildUI()
    {
        if (!ModificationLock.IsHeldByCurrentThread)
            throw new SynchronizationLockException(
                "RebuildUI must be called within a modification batch (between BeginModificationBatch and EndModificationBatch).");

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


            // Restore the original fields of the current element from the cached element before applying any modifications
            if (childIterator == 0)
            {
                if (currentNode == RootNode)
                {
                    if (currentNode.CachedElement != null)
                    {
                        // Only release childArrayMo if it was a custom array from a previous rebuild, not the cached original
                        if (childArrayMo != null && childArrayMo.GetAddress() != currentNode.CachedElement.GetAddress())
                            childArrayMo.ReleaseIfGlobalized();
                        SetUIDataMo(currentNode.CachedElement);
                        childArrayMo = currentNode.CachedElement;
                    }
                }
                else if (currentNode.CachedElement != null && currentElementMo != null)
                {
                    childArrayMo?.ReleaseIfGlobalized();
                    var typeDef = app.training.TrainingMenuData.REFType;
                    currentElementMo.CopyFieldsFrom(currentNode.CachedElement, typeDef);
                    childArrayMo = (currentElementMo as IObject)?.GetField("_ChildData") as ManagedObject;
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

                // Get the child element MO from the array
                var childArraySys = childArrayMo?.As<_System.Array>();
                var childElementMo = childArraySys?.GetValue(childIndex) as ManagedObject;

                // Get the child Game Array MO
                var childGameArrayMo = (childElementMo as IObject)?.GetField("_ChildData") as ManagedObject;

                // Push CHILD node onto the stack
                nodeStack.Push((childNode, childElementMo, childGameArrayMo, 0));
            }
            // ------------------------------------------
            // POST-ORDER / PROCESSING PHASE
            // ------------------------------------------
            else
            {
                if (currentNode.CachedElement != null && (currentNode == RootNode || currentElementMo != null))
                {
                    var hasModifications = currentNode.ReorderRequest != null ||
                                           currentNode.Appends.Count > 0 ||
                                           currentNode.Hooks.Count > 0;

                    if (!hasModifications)
                    {
                        currentNode.CachedElement.ReleaseIfGlobalized();
                        currentNode.CachedElement = null;
                        continue;
                    }

                    var currentElement = currentElementMo?.As<app.training.TrainingMenuData>();

                    if (currentNode.Hooks.Count > 0 && currentElement != null)
                    {
                        var functionId = FunctionTypeRegistry.RegisterGameFunctionType(currentElement.FuncType);
                        foreach (var hookRequest in currentNode.Hooks) hookRequest.AddHook(functionId);
                    }

                    // Use childArrayMo so this works identically for both RootNode (_MenuData) and child nodes (_ChildData)
                    var originalChildArray = childArrayMo!.As<app.training.TrainingMenuData_Array1D>()!;
                    var originalChildLength = originalChildArray.Length;

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
                            (uint)(originalChildLength + allNewElements.Sum(list => list.Count)));
                    newArrMo.Globalize();
                    var newArr = newArrMo.As<app.training.TrainingMenuData_Array1D>();

                    var newIndex = 0;
                    foreach (var newElement in allNewElements[0]) newArr[newIndex++] = newElement;

                    for (var targetIndex = 0; targetIndex < originalChildLength; targetIndex++)
                    {
                        var originalIndex = currentNode.ReorderRequest?.GetMapping(targetIndex) ?? targetIndex;
                        newArr[newIndex++] = originalChildArray[originalIndex];

                        foreach (var newElement in allNewElements[originalIndex + 1]) newArr[newIndex++] = newElement;
                    }

                    foreach (var newElement in allNewElements[originalChildLength + 1]) newArr[newIndex++] = newElement;

                    // Assign back to _UIData._MenuData for RootNode, or _ChildData for regular elements
                    if (currentNode == RootNode)
                        SetUIDataMo(newArrMo);
                    else
                        currentElement!.ChildData = newArr;
                }
            }
        }
    }

    public static void RegisterModification(List<int> traversalPath, IUIModificationRequest modificationRequest)
    {
        if (!ModificationLock.IsHeldByCurrentThread)
            throw new InvalidOperationException("RegisterModification must be called inside a ModifyUI call.");

        var currentNode = RootNode;
        // If RootNode was already modified, start traversal from the cached original _MenuData array
        var gameUIDataMo = RootNode.CachedElement ?? GetUIDataMo();
        ManagedObject? targetElementMo = null;

        // Traverse down to the target node (runs 0 times if traversalPath is empty)
        foreach (var index in traversalPath)
        {
            var gameUIDataArray = gameUIDataMo?.As<_System.Array>()
                                  ?? throw new InvalidCastException("UIData is not a valid array. Cannot traverse.");

            var childNode = currentNode[index];
            if (childNode == null)
            {
                childNode = new UICachedDataNode(null);
                currentNode[index] = childNode;
            }

            targetElementMo = childNode.CachedElement
                              ?? gameUIDataArray.GetValue(index) as ManagedObject
                              ?? throw new InvalidOperationException(
                                  $"Element at index {index} is null or not a ManagedObject. Cannot copy fields.");

            gameUIDataMo = (targetElementMo as IObject)?.GetField("_ChildData") as ManagedObject;
            currentNode = childNode;
        }

        // Cache the original data on the target node if not cached yet
        if (currentNode.CachedElement == null)
        {
            if (currentNode == RootNode)
            {
                // For RootNode, cache the original _MenuData array itself
                var originalMenuDataMo = GetUIDataMo()
                                         ?? throw new InvalidOperationException("_MenuData is null.");
                originalMenuDataMo.Globalize();
                RootNode.CachedElement = originalMenuDataMo;
            }
            else
            {
                var typeDef = app.training.TrainingMenuData.REFType;
                var elementCopyMo = typeDef.CreateInstance(0);
                elementCopyMo.Globalize();
                elementCopyMo.CopyFieldsFrom(targetElementMo!, typeDef);
                currentNode.CachedElement = elementCopyMo;
            }
        }

        // Register the request on currentNode (which is RootNode when traversalPath is empty)
        var success = modificationRequest switch
        {
            ReorderRequest reorderRequest => currentNode.RequestReorder(reorderRequest),
            PauseMenuAppendElementsRequest appendRequest => currentNode.RequestAppend(appendRequest),
            IUIHookRequest hookRequest when currentNode != RootNode => currentNode.RequestHook(hookRequest),
            _ => throw new InvalidOperationException(
                $"Unknown or invalid modification request type: {modificationRequest.GetType().Name}")
        };

        if (success)
        {
            ModificationRequestPaths[modificationRequest] = traversalPath;
            if (modificationRequest is ExclusiveModificationRequest singleUseRequest) singleUseRequest.Claim();
        }
        else
        {
            API.LogWarning(
                $"Modification request of type {modificationRequest.GetType().Name} is already registered at this path. Cannot register again.");
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
            case ReorderRequest:
                currentNode.ClearReorderRequest();
                break;
            case PauseMenuAppendElementsRequest appendRequest:
                currentNode.ClearAppendRequest(appendRequest);
                break;
            case IUIHookRequest hookRequest:
                currentNode.ClearDHookRequest(hookRequest);
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

    /// <summary>
    ///     Clears all registered custom function types and their associated delegates in the Training Pause Menu system.
    /// </summary>
    internal static void ClearTrainingPauseMenuHooks()
    {
        lock (ModificationLock)
        {
            foreach (var request in ModificationRequestPaths.Keys)
                if (request is ExclusiveModificationRequest singleUseRequest)
                    singleUseRequest.ReleaseClaim();

            ModificationRequestPaths.Clear();
            RootNode.Clear();

            // Clear all registered custom function types and their associated delegates
            FunctionTypeRegistry.Clear();
            TrainingFunctionHooks.Clear();
            SpinBoxHook.Clear();
            InputGuideHook.Clear();
        }
    }

    private class UICachedDataNode(ManagedObject? cachedElement)
    {
        private static readonly Comparer<(int index, UICachedDataNode node)> Comparer =
            Comparer<(int index, UICachedDataNode node)>.Create((a, b) => a.index.CompareTo(b.index));

        public ManagedObject? CachedElement { get; set; } = cachedElement;

        private List<(int, UICachedDataNode)> ChildNodes { get; } = []; // child nodes for the tree structure
        public System.Collections.Generic.IReadOnlyList<(int Index, UICachedDataNode Node)> Children => ChildNodes;

        public ReorderRequest? ReorderRequest { get; private set; } // optional reorder request

        private List<PauseMenuAppendElementsRequest> AppendRequests { get; } = []; // list of append requests
        public System.Collections.Generic.IReadOnlyList<PauseMenuAppendElementsRequest> Appends => AppendRequests;

        private List<IUIHookRequest> HookRequests { get; } = []; // list of hook requests
        public System.Collections.Generic.IReadOnlyList<IUIHookRequest> Hooks => HookRequests;

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

        public bool RequestAppend(PauseMenuAppendElementsRequest appendRequest)
        {
            AppendRequests.Add(appendRequest);
            return true;
        }

        public void ClearAppendRequest(PauseMenuAppendElementsRequest appendRequest)
        {
            appendRequest.Reset();
            AppendRequests.Remove(appendRequest);
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
        ///     Clears the cached element and all child nodes, as well as any registered modification requests (reorder, append,
        ///     hook) for this node and its children.
        /// </summary>
        public void Clear()
        {
            foreach (var (_, childNode) in ChildNodes) childNode.Clear();
            ChildNodes.Clear();

            ClearReorderRequest();
            foreach (var appendRequest in AppendRequests.ToList()) ClearAppendRequest(appendRequest);
            foreach (var hookRequest in HookRequests.ToList()) ClearDHookRequest(hookRequest);

            CachedElement?.ReleaseIfGlobalized();
            CachedElement = null;
        }
    }
}