using SF6_Plugin_Core.UI.OptionsSettings.Core;

namespace SF6_Plugin_Core.UI.OptionsSettings.Hooks;

public class ValueTypeHooks
{
    public delegate void ValueTypeDelegate(app.Option.ValueType valueType, int value, bool isReset);
    
    private static readonly ConcurrentDictionary<Guid, ValueTypeDelegate> CustomValueTypes = [];
    
    internal static void RegisterCustomValueType(Guid id, ValueTypeDelegate action)
    {
        if (OptionTypeRegistry.TryGetOptionType(id, out var _))
        {
            CustomValueTypes.TryAdd(id, action);
        }
        else
        {
            throw new InvalidOperationException($"Custom ValueType {id} is not registered. Call RegisterNewOptionType first.");
        }
    }

    internal static void UnregisterCustomValueType(Guid id)
    {
        CustomValueTypes.TryRemove(id, out var _);
    }

    [MethodHook(typeof(app.Option), "UpdatedOptionValueEvent(app.Option.ValueType, System.Int32, System.Boolean)", MethodHookType.Pre)]
    public static PreHookResult OnValueTypeEvent(Span<ulong> args)
    {
        var valueType = (int)args[1];

        if (valueType <= OptionTypeRegistry.OptionTypeMax) return PreHookResult.Continue;
        
        if (!OptionTypeRegistry.TryGetOptionId(valueType, out var id)) return PreHookResult.Continue;

        if (CustomValueTypes.TryGetValue(id, out var action))
        {
            var value = (int) args[2];
            var isReset = args[3] == 1;
            
            action.Invoke((app.Option.ValueType) valueType, value, isReset);
            return PreHookResult.Skip;
        }
        return PreHookResult.Continue;
    }
}