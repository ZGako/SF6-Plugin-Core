using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;


public class SpinBoxHookRequest(SpinBoxHook.SpinBoxInitDelegate action) : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly SpinBoxHook.SpinBoxInitDelegate _action = action;

    private Guid _functionId;

    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        SpinBoxHook.RegisterCustomSpinBoxInitialization(functionId, _action);
    }

    public void ClearHook()
    {
        SpinBoxHook.UnregisterCustomSpinBoxInitialization(_functionId);
    }

    public override SpinBoxHookRequest Clone()
    {
        return new SpinBoxHookRequest(_action);
    }
}