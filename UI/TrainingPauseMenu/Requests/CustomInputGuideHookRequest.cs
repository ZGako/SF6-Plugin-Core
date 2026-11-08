using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

public class CustomInputGuideHookRequest(
    app.InputAssign.Digital.Id inputId,
    float xPos = CustomInputGuideHook.DefaultCustomTextBaseX)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private Guid _functionId;

    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        CustomInputGuideHook.RegisterCustomSecondaryList(functionId, inputId, xPos);
    }

    public void ClearHook()
    {
        CustomInputGuideHook.UnregisterCustomSecondaryList(_functionId);
    }

    public override CustomInputGuideHookRequest Clone()
    {
        return new CustomInputGuideHookRequest(inputId, xPos);
    }
}