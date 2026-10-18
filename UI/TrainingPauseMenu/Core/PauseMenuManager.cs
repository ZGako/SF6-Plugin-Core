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
                if (currentNode.CachedElement != null && currentElementMo != null)
                {
                    // released the childArray of the ingame object, since we might've modified it (by creating a new one) and we don't want to leak memory when reassigning it the cached one.
                    childArrayMo?.ReleaseIfGlobalized();

                    var typeDef = app.training.TrainingMenuData.REFType;

                    currentElementMo.CopyFieldsFrom(currentNode.CachedElement, typeDef);

                    childArrayMo = (currentElementMo as IObject)?.GetField("_ChildData") as ManagedObject;
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
                if (currentNode == RootNode) continue; // Skip root as it doesn't represent an element


                if (currentNode.CachedElement != null && currentElementMo != null)
                {
                    // If there are no modifications, we can release the cached element to free up memory (only after we restored it, which we just did)

                    var hasModifications = currentNode.ReorderRequest != null ||
                                           currentNode.Appends.Count > 0 ||
                                           currentNode.Hooks.Count > 0;
                    if (!hasModifications)
                    {
                        // Release logic
                        currentNode.CachedElement.ReleaseIfGlobalized();
                        currentNode.CachedElement = null;
                        // API.LogInfo($"Released cached element at node with {currentNode.Children.Count} children as it has no modifications.");
                        continue;
                    }

                    var currentElement = currentElementMo.As<app.training.TrainingMenuData>();

                    // Apply the hook requests to the current element.

                    // in this loop we only ever add hooks for original game elements, because of this we want to register the 
                    // functionType in the function registry ourselves
                    if (currentNode.Hooks.Count > 0)
                    {
                        var functionId = FunctionTypeRegistry.RegisterGameFunctionType(currentElement.FuncType);
                        foreach (var hookRequest in currentNode.Hooks) hookRequest.AddHook(functionId);
                    }

                    // Gather all the appendElement requests inside a single array of lists
                    var originalChildLength = currentElement.ChildData.Length;
                    var allNewElements = new List<app.training.TrainingMenuData>[originalChildLength + 2];
                    // initialize the lists to avoid null reference exceptions
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

                    // Create a new array
                    var newArrMo =
                        app.training.TrainingMenuData.REFType.CreateManagedArray(
                            (uint)(originalChildLength + allNewElements.Sum(list => list.Count)));
                    newArrMo.Globalize();

                    var newArr = newArrMo.As<app.training.TrainingMenuData_Array1D>();

                    // Copy the original elements and append the new elements after each one.
                    // if there is a ReorderRequest, we reorder the original elements according to the new order whilst interleaving the new elements at their respective original indices.
                    var newIndex = 0;

                    // first we insert the elements at index 0 (which go before the first original element)
                    foreach (var newElement in allNewElements[0]) newArr[newIndex++] = newElement;

                    for (var targetIndex = 0; targetIndex < originalChildLength; targetIndex++)
                    {
                        var originalIndex = currentNode.ReorderRequest?.GetMapping(targetIndex) ?? targetIndex;

                        // Copy the original element to the new array at the mapped index
                        newArr[newIndex++] = currentElement.ChildData[originalIndex];

                        // Append any new elements for this original index
                        foreach (var newElement in allNewElements[originalIndex + 1]) newArr[newIndex++] = newElement;
                    }

                    // insert the elements at the end of the array (which go after the last element)
                    foreach (var newElement in allNewElements[originalChildLength + 1]) newArr[newIndex++] = newElement;

                    // Replace the original array with the new array
                    currentElement.ChildData = newArr;
                }
            }
        }
    }

    public static void RegisterModification(List<int> traversalPath, IUIModificationRequest modificationRequest)
    {
        // Add the modification request to the parent node's list

        if (!ModificationLock.IsHeldByCurrentThread)
            throw new InvalidOperationException("RegisterModification must be called inside a ModifyUI call.");

        var currentNode = RootNode;
        var gameUIDataMo = GetUIDataMo();

        // traverse both trees and cache 
        for (var i = 0; i < traversalPath.Count; i++)
        {
            var index = traversalPath[i];

            var gameUIDataArray = gameUIDataMo?.As<_System.Array>() ??
                                  throw new InvalidCastException("UIData is not a valid array. Cannot traverse.");

            // get the cached node at the current index

            // node should never be null here, but the compiler complains anyway
            if (currentNode == null)
                throw new InvalidOperationException($"Current node is null at index {index}. Cannot traverse further.");

            var childNode = currentNode[index];
            if (childNode == null)
            {
                childNode = new UICachedDataNode(null); // Replace with your actual variables
                currentNode[index] = childNode;
            }

            if (i == traversalPath.Count - 1)
            {
                currentNode[index] ??= new UICachedDataNode(null);

                // cache the element by copying the entire element at the current index

                if (currentNode[index]!.CachedElement == null)
                {
                    var elementCopyMo = app.training.TrainingMenuData.REFType.CreateInstance(0);
                    var typeDef = app.training.TrainingMenuData.REFType;
                    elementCopyMo.Globalize();

                    var originalElementMo = gameUIDataArray.GetValue(index) as ManagedObject
                                            ?? throw new InvalidOperationException(
                                                $"Element at index {index} is null or not a ManagedObject. Cannot copy fields.");

                    elementCopyMo.CopyFieldsFrom(originalElementMo, typeDef);

                    // store the copied element in the cached node
                    currentNode[index]!.CachedElement = elementCopyMo;
                }

                var success = modificationRequest switch
                {
                    ReorderRequest reorderRequest => currentNode[index]!.RequestReorder(reorderRequest),
                    PauseMenuAppendElementsRequest appendRequest => currentNode[index]!.RequestAppend(appendRequest),
                    IUIHookRequest hookRequest => currentNode[index]!.RequestHook(hookRequest),
                    _ => throw new InvalidOperationException(
                        $"Unknown modification request type: {modificationRequest.GetType().Name}")
                };
                if (success)
                {
                    ModificationRequestPaths[modificationRequest] = traversalPath;
                    if (modificationRequest is ExclusiveModificationRequest singleUseRequest) singleUseRequest.Claim();
                }
                else
                {
                    API.LogWarning(
                        $"Modification request of type {modificationRequest.GetType().Name} is already registered at index {index}. Cannot register again.");
                }

                return;
            }

            // get the UIData note
            var nextElementMo = childNode.CachedElement ?? gameUIDataArray.GetValue(index) as ManagedObject;
            gameUIDataMo = (nextElementMo as IObject)?.GetField("_ChildData") as ManagedObject;

            currentNode = childNode;
        }
    }

    public static void UnregisterModification(IUIModificationRequest modificationRequest)
    {
        if (!ModificationLock.IsHeldByCurrentThread)
            throw new InvalidOperationException("UnregisterModification must be called inside a ModifyUI call.");
        // Remove the modification request from the parent node's list

        if (!ModificationRequestPaths.TryRemove(modificationRequest, out var traversalPath)) return;

        if (modificationRequest is ExclusiveModificationRequest singleUseRequest) singleUseRequest.ReleaseClaim();

        // traverse the cached tree to find the modification request and remove it
        var currentNode = RootNode;
        for (var i = 0; i < traversalPath.Count; i++)
        {
            var index = traversalPath[i];

            if (i == traversalPath.Count - 1)
            {
                if (currentNode[index] == null)
                {
                    API.LogWarning($"Cached node at index {index} is null. Cannot unregister modification request.");
                    return;
                }

                switch (modificationRequest)
                {
                    case ReorderRequest _:
                        currentNode[index]!.ClearReorderRequest();
                        break;
                    case PauseMenuAppendElementsRequest appendRequest:
                        currentNode[index]!.ClearAppendRequest(appendRequest);
                        break;
                    case IUIHookRequest hookRequest:
                        currentNode[index]!.ClearDHookRequest(hookRequest);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown modification request type: {modificationRequest.GetType().Name}");
                }

                return;
            }

            // traverse to the next child node
            if (currentNode[index] == null)
            {
                API.LogWarning($"Cached node at index {index} is null. Cannot traverse further.");
                return;
            }

            currentNode = currentNode[index]!;
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