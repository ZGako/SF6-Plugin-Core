using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

public class UICustomElementNode(Guid functionId, IUICustomElementInitialization customInitializer)
{
    private readonly List<UICustomElementNode> _childNodes = [];
    private readonly IUICustomElementInitialization _customInitializer = customInitializer;
    private readonly Guid _functionId = functionId;

    private readonly List<IUIHookRequest> _hookInstructions = [];

    private ManagedObject? _childDataArrayMo;

    public void AddChildren(List<UICustomElementNode> children)
    {
        _childNodes.AddRange(children);
    }

    public void RemoveChildren(List<UICustomElementNode> children)
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

    internal app.training.TrainingMenuData Build()
    {
        // call the custom initializer to initialize the custom element
        // this way the function types will be numbered similarly to the game (doesn't actually matter)
        var customElementData = _customInitializer.InitializeCustomElement(_functionId);

        _childDataArrayMo?.ReleaseIfGlobalized();
        _childDataArrayMo = null;

        // now we traverse the child nodes recursively and build their data
        // on the way up we add the hooks and create the childData arrays
        if (_childNodes.Count > 0)
        {
            // create a new array
            _childDataArrayMo = app.training.TrainingMenuData.REFType.CreateManagedArray((uint)_childNodes.Count);
            _childDataArrayMo.Globalize();

            var childDataArray = _childDataArrayMo.As<app.training.TrainingMenuData_Array1D>();

            for (var i = 0; i < _childNodes.Count; i++)
            {
                var childData = _childNodes[i].Build();
                childDataArray[i] = childData;
            }

            // assign the childData array to the custom element
            customElementData.ChildData = childDataArray;
        }

        // add the hooks to the custom element
        foreach (var hook in _hookInstructions) hook.AddHook(_functionId);

        return customElementData;
    }

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