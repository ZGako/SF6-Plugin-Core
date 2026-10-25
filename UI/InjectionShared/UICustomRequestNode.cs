namespace SF6_Plugin_Core.UI.InjectionShared;

public abstract class UICustomRequestNode<TType, TInstantiation>(Guid functionId, TInstantiation customInitializer)
    where TInstantiation : IUICustomElementInitialization<TType>
{
    protected readonly List<UICustomRequestNode<TType, TInstantiation>> ChildNodes = [];
    protected readonly TInstantiation CustomInitializer = customInitializer;
    protected readonly Guid FunctionId = functionId;

    protected readonly List<IUIHookRequest> HookInstructions = [];

    protected ManagedObject? ChildDataArrayMo;

    public void AddChildren(List<UICustomRequestNode<TType, TInstantiation>> children)
    {
        ChildNodes.AddRange(children);
    }

    public void RemoveChildren(List<UICustomRequestNode<TType, TInstantiation>> children)
    {
        foreach (var child in children)
            if (ChildNodes.Remove(child))
                child.Clear();
    }

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

    public void RemoveHook(IUIHookRequest hookRequest)
    {
        if (HookInstructions.Remove(hookRequest)) hookRequest.ClearHook();
    }

    internal abstract TType Build();

    /// this is the full clear method that's called when the element gets completely removed from the tree
    internal void Clear()
    {
        // traverse the children and clear them
        foreach (var child in ChildNodes) child.Clear();
        ChildDataArrayMo?.ReleaseIfGlobalized();
        ChildDataArrayMo = null;

        CustomInitializer.Clear();

        foreach (var hook in HookInstructions) hook.ClearHook();
    }
}