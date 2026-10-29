namespace SF6_Plugin_Core.UI.OptionsSettings.Core;

public static class OptionsHelpers
{
    public static app.UIPartsOptionUnit GetPartsOptionUnit(this app.UIPartsGroup partsGroup, Guid customOptionId)
    {
        if (!OptionTypeRegistry.TryGetOptionType(customOptionId, out var optionType))
            throw new InvalidOperationException($"{customOptionId} has not been registered yet");

        for (var i = 0; i < partsGroup._Children.Count; i++)
        {
            var part = partsGroup._Children[i];
            var uiPart = ManagedProxy<app.UIPartsOptionUnit>.Create(part);
            if (uiPart.UnitData.Setting.TypeId == optionType) return uiPart;
        }

        throw new InvalidOperationException($"Cannot find UI Part associated Id {customOptionId}");
    }

    public static bool TryGetPartsOptionUnit(this app.UIPartsGroup partsGroup, Guid customOptionId,
        out app.UIPartsOptionUnit? result)
    {
        result = null;

        if (!OptionTypeRegistry.TryGetOptionType(customOptionId, out var optionType)) return false;

        for (var i = 0; i < partsGroup._Children.Count; i++)
        {
            var part = partsGroup._Children[i];
            var uiPart = ManagedProxy<app.UIPartsOptionUnit>.Create(part);
            API.LogInfo($"testing for {uiPart.UnitData.Setting.TypeId} and {optionType}");
            if (uiPart.UnitData.Setting.TypeId == optionType)
            {
                result = uiPart;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetOptionId(int type, out Guid optionId)
    {
        return OptionTypeRegistry.TryGetOptionId(type, out optionId);
    }
}