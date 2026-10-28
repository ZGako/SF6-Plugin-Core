using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.OptionsSettings.Hooks;

namespace SF6_Plugin_Core.UI.OptionsSettings.Requests;

public class OnValueTypeEventHookRequest(ValueTypeHooks.ValueTypeDelegate valueTypeDelegate) :
    ExclusiveModificationRequest, IUIHookRequest
{
    private Guid _valueTypeId;
    
    public override IUIModificationRequest Clone()
    {
        return new OnValueTypeEventHookRequest(valueTypeDelegate);
    }

    public void AddHook(Guid functionNameId)
    {
        _valueTypeId = functionNameId;
        ValueTypeHooks.RegisterCustomValueType(functionNameId, valueTypeDelegate);
    }

    public void ClearHook()
    {
        ValueTypeHooks.UnregisterCustomValueType(_valueTypeId);
    }
}