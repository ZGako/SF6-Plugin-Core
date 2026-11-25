using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.OptionsSettings.Hooks;

namespace SF6_Plugin_Core.UI.OptionsSettings.Requests;

/// <summary>
///     Attaches a value-change/reset handler (<see cref="ValueTypeHooks.ValueTypeDelegate" />) to a
///     <see cref="CustomOptions.UICustomOptionsValueNode" /> via <c>AddHook</c>.
/// </summary>
/// <param name="valueTypeDelegate">Delegate invoked when the user alters the option value or resets Options to defaults.</param>
public class OnValueTypeEventHookRequest(ValueTypeHooks.ValueTypeDelegate valueTypeDelegate) :
    ExclusiveModificationRequest, IUIHookRequest
{
    private Guid _valueTypeId;

    /// <inheritdoc />
    public override IUIModificationRequest Clone()
    {
        return new OnValueTypeEventHookRequest(valueTypeDelegate);
    }

    /// <inheritdoc />
    public void AddHook(Guid functionNameId)
    {
        _valueTypeId = functionNameId;
        ValueTypeHooks.RegisterCustomValueType(functionNameId, valueTypeDelegate);
    }

    /// <inheritdoc />
    public void ClearHook()
    {
        ValueTypeHooks.UnregisterCustomValueType(_valueTypeId);
    }
}