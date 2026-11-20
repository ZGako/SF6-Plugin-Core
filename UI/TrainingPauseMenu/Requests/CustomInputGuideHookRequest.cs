using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

public class CustomInputGuideHookRequest(
    app.InputAssign.Digital.Id inputId1,
    app.InputAssign.Digital.Id inputId2 = app.InputAssign.Digital.Id.Invalid,
    app.InputAssign.Digital.Id inputId3 = app.InputAssign.Digital.Id.Invalid,
    float xPos = CustomInputGuideHook.DefaultCustomTextBaseX)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private Guid _functionId;

    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        CustomInputGuideHook.RegisterCustomSecondaryList(functionId, inputId1, inputId2, inputId3, xPos);
    }

    public void ClearHook()
    {
        CustomInputGuideHook.UnregisterCustomSecondaryList(_functionId);
    }

    public override CustomInputGuideHookRequest Clone()
    {
        return new CustomInputGuideHookRequest(inputId1, inputId2, inputId3, xPos);
    }
}