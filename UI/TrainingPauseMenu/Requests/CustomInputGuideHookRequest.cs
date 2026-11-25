using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

/// <summary>
///     Attaches up to three floating digital button-prompt tags (displayed by <see cref="CustomInputGuideHook" /> when
///     the target row is focused in a secondary menu list) to either a custom node or an existing vanilla
///     <see cref="app.training.TrainingMenuData" /> item.
/// </summary>
/// <param name="inputId1">Primary digital input prompt icon displayed on focus.</param>
/// <param name="inputId2">Optional second input icon joined with <c>" / "</c>.</param>
/// <param name="inputId3">Optional third input icon joined with <c>" / "</c>.</param>
/// <param name="xPos">Local X offset of the cloned prompt text (defaults to <see cref="CustomInputGuideHook.DefaultCustomTextBaseX" />).</param>
public class CustomInputGuideHookRequest(
    app.InputAssign.Digital.Id inputId1,
    app.InputAssign.Digital.Id inputId2 = app.InputAssign.Digital.Id.Invalid,
    app.InputAssign.Digital.Id inputId3 = app.InputAssign.Digital.Id.Invalid,
    float xPos = CustomInputGuideHook.DefaultCustomTextBaseX)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private Guid _functionId;

    /// <inheritdoc />
    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        CustomInputGuideHook.RegisterCustomSecondaryList(functionId, inputId1, inputId2, inputId3, xPos);
    }

    /// <inheritdoc />
    public void ClearHook()
    {
        CustomInputGuideHook.UnregisterCustomSecondaryList(_functionId);
    }

    /// <inheritdoc />
    public override CustomInputGuideHookRequest Clone()
    {
        return new CustomInputGuideHookRequest(inputId1, inputId2, inputId3, xPos);
    }
}