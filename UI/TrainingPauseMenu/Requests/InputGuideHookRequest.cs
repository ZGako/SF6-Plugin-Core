using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

/// <summary>
///     Attaches a bottom-bar <see cref="InputGuideHook.InputGuideDelegate" /> customization to a vanilla or custom
///     Training Pause Menu element.
/// </summary>
/// <param name="action">Delegate invoked after SF6 populates footer guide prompts for the target item.</param>
public class InputGuideHookRequest(InputGuideHook.InputGuideDelegate action)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly InputGuideHook.InputGuideDelegate _action = action;

    private Guid _functionId;

    /// <inheritdoc />
    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        InputGuideHook.RegisterCustomInputGuideFunction(functionId, _action);
    }

    /// <inheritdoc />
    public void ClearHook()
    {
        InputGuideHook.UnregisterCustomInputGuideFunction(_functionId);
    }

    /// <inheritdoc />
    public override IUIModificationRequest Clone()
    {
        return new InputGuideHookRequest(_action);
    }
}