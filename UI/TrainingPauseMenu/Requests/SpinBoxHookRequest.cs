using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

/// <summary>
///     Attaches a <see cref="SpinBoxHook.SpinBoxInitDelegate" /> to a vanilla or custom Training Pause Menu
///     spinner row so SF6 displays the choice matching mod state when opening the menu.
/// </summary>
/// <param name="action">Delegate returning the active child item's <see cref="app.training.TrainingFuncType" /> or custom <see cref="Guid" />.</param>
public class SpinBoxHookRequest(SpinBoxHook.SpinBoxInitDelegate action) : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly SpinBoxHook.SpinBoxInitDelegate _action = action;

    private Guid _functionId;

    /// <inheritdoc />
    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        SpinBoxHook.RegisterCustomSpinBoxInitialization(functionId, _action);
    }

    /// <inheritdoc />
    public void ClearHook()
    {
        SpinBoxHook.UnregisterCustomSpinBoxInitialization(_functionId);
    }

    /// <inheritdoc />
    public override SpinBoxHookRequest Clone()
    {
        return new SpinBoxHookRequest(_action);
    }
}