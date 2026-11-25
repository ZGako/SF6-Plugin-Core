namespace SF6_Plugin_Core.UI.InjectionShared;

/// <summary>
///     Declarative node in a mod-defined UI subtree pairing an element initializer with child nodes and runtime hook
///     bindings.
/// </summary>
/// <typeparam name="TType">The RE Engine UI data proxy type materialized by this node.</typeparam>
/// <typeparam name="TInstantiation">The concrete initializer type creating <typeparamref name="TType" />.</typeparam>
/// <param name="functionId">
///     Stable <see cref="Guid" /> mapped dynamically on each rebuild to a fresh custom <c>FuncType</c> or
///     <c>ValueType</c>/<c>GroupType</c> integer.
/// </param>
/// <param name="customInitializer">Initializer responsible for building and owning the node's engine UI object.</param>
public abstract class UICustomRequestNode<TType, TInstantiation>(Guid functionId, TInstantiation customInitializer)
    where TInstantiation : IUICustomElementInitialization<TType>
{
    /// <summary>Ordered child request nodes built beneath this node when constructing submenus or spinner items.</summary>
    protected readonly List<UICustomRequestNode<TType, TInstantiation>> ChildNodes = [];

    /// <summary>Initializer that constructs and tracks the native RE Engine UI data object for this node.</summary>
    protected readonly TInstantiation CustomInitializer = customInitializer;

    /// <summary>Stable identifier used to register this node's runtime type and bind its hooks across rebuilds.</summary>
    protected readonly Guid FunctionId = functionId;

    /// <summary>
    ///     Returns the native RE Engine element constructed for this node, enabling live mutation (such as toggling
    ///     <c>IsEnabled</c>) without triggering a full menu rebuild.
    /// </summary>
    /// <returns>The initialized <typeparamref name="TType" /> proxy owned by <see cref="CustomInitializer" />.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the UI tree containing this node has not been built yet or has been cleared.</exception>
    public TType GetBuiltElement()
    {
        return CustomInitializer.GetElementData();
    }

    /// <summary>Hooks attached to this node, registered on every <c>Build()</c> after the element's ID is allocated.</summary>
    protected readonly List<IUIHookRequest> HookInstructions = [];

    /// <summary>Globalized native child array assigned to the built element on the most recent build, if applicable.</summary>
    protected ManagedObject? ChildDataArrayMo;

    /// <summary>
    ///     Appends child nodes (such as spinner options, submenu rows, or option units under a group) built beneath
    ///     this element on subsequent rebuilds.
    /// </summary>
    /// <param name="children">The ordered list of child nodes to attach.</param>
    public void AddChildren(List<UICustomRequestNode<TType, TInstantiation>> children)
    {
        ChildNodes.AddRange(children);
    }

    /// <summary>
    ///     Removes the specified child nodes from this node and immediately clears their hooks and native resources.
    /// </summary>
    /// <param name="children">Child nodes to detach and dispose.</param>
    public void RemoveChildren(List<UICustomRequestNode<TType, TInstantiation>> children)
    {
        foreach (var child in children)
            if (ChildNodes.Remove(child))
                child.Clear();
    }

    /// <summary>
    ///     Attaches a hook request (for example <c>FunctionHookRequest</c> or <c>SpinBoxHookRequest</c>) to be bound
    ///     to <see cref="FunctionId" /> whenever this node is built.
    /// </summary>
    /// <remarks>
    ///     At most one hook of each concrete <see cref="IUIHookRequest" /> subclass may be attached to the same node.
    /// </remarks>
    /// <param name="hookRequest">The hook instruction to bind on build.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when a hook request of the exact same concrete type is already attached to this node.
    /// </exception>
    public void AddHook(IUIHookRequest hookRequest)
    {
        var incomingType = hookRequest.GetType();
        var hasSameHookClass = HookInstructions.Any(r => r.GetType() == incomingType);

        if (hasSameHookClass)
            // A hook request of this specific concrete class already exists.
            throw new InvalidOperationException(
                $"A hook request of type {incomingType.Name} is already registered for this custom element. Cannot register multiple hooks of the same type.");

        HookInstructions.Add(hookRequest);
    }

    /// <summary>
    ///     Detaches <paramref name="hookRequest" /> from this node and unregisters its runtime hook if previously bound.
    /// </summary>
    /// <param name="hookRequest">The previously attached hook instruction to remove.</param>
    public void RemoveHook(IUIHookRequest hookRequest)
    {
        if (HookInstructions.Remove(hookRequest)) hookRequest.ClearHook();
    }

    internal abstract TType Build();

    /// <summary>
    ///     Unregisters C# hooks on this node and its descendants immediately (does not release native objects).
    /// </summary>
    internal void ClearHooks()
    {
        foreach (var child in ChildNodes) child.ClearHooks();
        foreach (var hook in HookInstructions) hook.ClearHook();
    }

    /// <summary>
    ///     Releases native/engine objects held by this node and its descendants.
    /// </summary>
    internal void ClearNativeResources()
    {
        foreach (var child in ChildNodes) child.ClearNativeResources();
        ChildDataArrayMo?.ReleaseIfGlobalized();
        ChildDataArrayMo = null;

        CustomInitializer.Clear();
    }

    /// this is the full clear method that's called when the element gets completely removed from the tree
    internal void Clear()
    {
        ClearHooks();
        ClearNativeResources();
    }
}