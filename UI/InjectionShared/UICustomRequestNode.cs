using SF6_Plugin_Core.UI.InjectionShared;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

public abstract class UICustomRequestNode<TType, TInstantiation>(Guid functionId, TInstantiation customInitializer)
    where TInstantiation : IUICustomElementInitialization<TType>
{
    protected readonly List<UICustomRequestNode<TType, TInstantiation>> _childNodes = [];
    protected readonly TInstantiation _customInitializer = customInitializer;
    protected readonly Guid _functionId = functionId;

    protected readonly List<IUIHookRequest> _hookInstructions = [];

    protected ManagedObject? _childDataArrayMo;

    public void AddChildren(List<UICustomRequestNode<TType, TInstantiation>> children)
    {
        _childNodes.AddRange(children);
    }

    public void RemoveChildren(List<UICustomRequestNode<TType, TInstantiation>> children)
    {
        foreach (var child in children)
            if (_childNodes.Remove(child))
                child.Clear();
    }

    public void AddHook(IUIHookRequest hookRequest)
    {
        var incomingType = hookRequest.GetType();
        var hasSameHookClass = _hookInstructions.Any(r => r.GetType() == incomingType);

        if (hasSameHookClass)
            // A hook request of this specific concrete class already exists.
            throw new InvalidOperationException(
                $"A hook request of type {incomingType.Name} is already registered for this custom element. Cannot register multiple hooks of the same type.");

        _hookInstructions.Add(hookRequest);
    }

    public void RemoveHook(IUIHookRequest hookRequest)
    {
        if (_hookInstructions.Remove(hookRequest)) hookRequest.ClearHook();
    }

    internal abstract TType Build();

    /// this is the full clear method that's called when the element gets completely removed from the tree
    internal void Clear()
    {
        // traverse the children and clear them
        foreach (var child in _childNodes) child.Clear();
        _childDataArrayMo?.ReleaseIfGlobalized();
        _childDataArrayMo = null;

        _customInitializer.Clear();

        foreach (var hook in _hookInstructions) hook.ClearHook();
    }
}