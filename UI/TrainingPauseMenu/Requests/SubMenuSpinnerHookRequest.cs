using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

/// <summary>
///     Attaches dynamic initialization, display formatting, optional bounds, and optional row visibility filtering
///     to a <c>SubMenuSpinnerInitializer</c> row inside a <c>SPINLIST</c> popup.
/// </summary>
/// <param name="initDelegate">Supplies the current backing integer value on submenu open and list refreshes.</param>
/// <param name="formatDelegate">Formats the domain integer into display text and marks whether it is non-default.</param>
/// <param name="limitDelegate">Optional dynamic <c>(Min, Max)</c> override evaluated on open/refresh.</param>
/// <param name="maskThisItemRow">Optional predicate that removes the spinner row from the popup when returning <see langword="true" />.</param>
public class SubMenuSpinnerHookRequest(
    SubMenuSpinnerHook.SubMenuSpinnerInitDelegate initDelegate,
    SubMenuSpinnerHook.SubMenuSpinnerFormatDelegate formatDelegate,
    SubMenuSpinnerHook.SubMenuSpinnerLimitDelegate? limitDelegate = null,
    SubMenuSpinnerHook.MaskThisItemRowDelegate? maskThisItemRow = null)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private Guid _functionId;

    /// <inheritdoc />
    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        SubMenuSpinnerHook.RegisterCustomSubMenuSpinner(functionId, initDelegate, formatDelegate, limitDelegate,
            maskThisItemRow);
    }

    /// <inheritdoc />
    public void ClearHook()
    {
        SubMenuSpinnerHook.UnregisterCustomSubMenuSpinner(_functionId);
    }

    /// <inheritdoc />
    public override SubMenuSpinnerHookRequest Clone()
    {
        return new SubMenuSpinnerHookRequest(initDelegate, formatDelegate, limitDelegate, maskThisItemRow);
    }
}