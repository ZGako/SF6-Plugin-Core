using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

//FIXME: refactor
public class SpinBoxHookRequest(SpinBoxHook.SpinBoxInitDelegate action) : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly SpinBoxHook.SpinBoxInitDelegate _action = action;

    private string? _functionName;

    public void AddDispatcher(string functionName)
    {
        _functionName = functionName;
        SpinBoxHook.RegisterCustomSpinBoxInitialization(functionName, _action);
    }

    public void ClearDispatcher()
    {
        if (_functionName is null)
        {
            API.LogError("Cannot clear dispatcher because it was never added.");
            throw new InvalidOperationException("Cannot clear dispatcher because it was never added.");
        }
        SpinBoxHook.UnregisterCustomSpinBoxInitialization(_functionName);
    }

    public override SpinBoxHookRequest Clone()
    {
        return new SpinBoxHookRequest(_action);
    }
}