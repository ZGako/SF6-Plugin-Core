using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

public class SubMenuSpinnerHookRequest(
    SubMenuSpinnerHook.SubMenuSpinnerInitDelegate initDelegate,
    SubMenuSpinnerHook.SubMenuSpinnerFormatDelegate formatDelegate,
    SubMenuSpinnerHook.SubMenuSpinnerLimitDelegate? limitDelegate = null,
    SubMenuSpinnerHook.MaskThisItemRowDelegate? maskThisItemRow = null)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private Guid _functionId;

    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        SubMenuSpinnerHook.RegisterCustomSubMenuSpinner(functionId, initDelegate, formatDelegate, limitDelegate,
            maskThisItemRow);
    }

    public void ClearHook()
    {
        SubMenuSpinnerHook.UnregisterCustomSubMenuSpinner(_functionId);
    }

    public override SubMenuSpinnerHookRequest Clone()
    {
        return new SubMenuSpinnerHookRequest(initDelegate, formatDelegate, limitDelegate, maskThisItemRow);
    }
}