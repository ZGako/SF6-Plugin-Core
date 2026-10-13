using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

//FIXME: refactor
public class InputGuideHookRequest(InputGuideHook.InputGuideDelegate action) : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly InputGuideHook.InputGuideDelegate _action = action;

    private string? _functionName;

    public void AddDispatcher(string functionName)
    {
        _functionName = functionName;
        InputGuideHook.RegisterCustomInputGuideFunction(functionName, _action);
    }

    public void ClearDispatcher()
    {
        if (_functionName is null)
        {
            API.LogError("Cannot clear dispatcher because it was never added.");
            throw new InvalidOperationException("Cannot clear dispatcher because it was never added.");
        }
        InputGuideHook.UnregisterCustomInputGuideFunction(_functionName);
    }

    public override IUIModificationRequest Clone()
    {
        return new InputGuideHookRequest(_action);
    }
}