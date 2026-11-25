using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.OptionsSettings.Hooks;

namespace SF6_Plugin_Core.UI.OptionsSettings.Requests;

/// <summary>
///     Attaches a dynamic disable predicate (<see cref="DisableReasonHooks.DisableConditionDelegate" />) to a
///     <see cref="CustomOptions.UICustomOptionRequestNode" /> via <c>AddHook</c>.
/// </summary>
/// <param name="conditionDelegate">Predicate evaluated on each disable-reason check; returning <see langword="true" /> disables the option.</param>
public class OnDisableReasonHookRequest(DisableReasonHooks.DisableConditionDelegate conditionDelegate) :
    ExclusiveModificationRequest, IUIHookRequest
{
    private Guid _optionId;

    /// <inheritdoc />
    public override IUIModificationRequest Clone()
    {
        return new OnDisableReasonHookRequest(conditionDelegate);
    }

    /// <inheritdoc />
    public void AddHook(Guid functionNameId)
    {
        _optionId = functionNameId;
        DisableReasonHooks.RegisterDisableCondition(functionNameId, conditionDelegate);
    }

    /// <inheritdoc />
    public void ClearHook()
    {
        DisableReasonHooks.UnregisterDisableCondition(_optionId);
    }
}