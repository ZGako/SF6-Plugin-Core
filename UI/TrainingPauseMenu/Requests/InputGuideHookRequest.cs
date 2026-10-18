using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

public class InputGuideHookRequest(InputGuideHook.InputGuideDelegate action)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly InputGuideHook.InputGuideDelegate _action = action;

    private Guid _functionId;

    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        InputGuideHook.RegisterCustomInputGuideFunction(functionId, _action);
    }

    public void ClearHook()
    {
        InputGuideHook.UnregisterCustomInputGuideFunction(_functionId);
    }

    public override IUIModificationRequest Clone()
    {
        return new InputGuideHookRequest(_action);
    }
}