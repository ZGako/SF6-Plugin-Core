using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.OptionsSettings.Hooks;

namespace SF6_Plugin_Core.UI.OptionsSettings.Requests;

public class OnDisableReasonHookRequest(DisableReasonHooks.DisableConditionDelegate conditionDelegate) :
    ExclusiveModificationRequest, IUIHookRequest
{
    private Guid _optionId;

    public override IUIModificationRequest Clone()
    {
        return new OnDisableReasonHookRequest(conditionDelegate);
    }

    public void AddHook(Guid functionNameId)
    {
        _optionId = functionNameId;
        DisableReasonHooks.RegisterDisableCondition(functionNameId, conditionDelegate);
    }

    public void ClearHook()
    {
        DisableReasonHooks.UnregisterDisableCondition(_optionId);
    }
}