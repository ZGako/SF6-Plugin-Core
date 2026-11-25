using SF6_Plugin_Core.UI.OptionsSettings.Core;

namespace SF6_Plugin_Core.UI.OptionsSettings.Hooks;

/// <summary>
///     Intercepts <see cref="app.Option.UpdatedOptionValueEvent" /> to dispatch value changes and reset-to-default
///     events for custom Options value nodes.
/// </summary>
public class ValueTypeHooks
{
    /// <summary>
    ///     Callback invoked when a custom Options slider, spinner, or On/Off toggle changes or is reset.
    /// </summary>
    /// <param name="valueType">The runtime custom integer cast to <see cref="app.Option.ValueType" />.</param>
    /// <param name="value">
    ///     The newly committed integer value (0-based choice index for <c>SpinText</c>, or <c>0 = On / 1 = Off</c> for
    ///     <c>SpinText_OnOff</c>).
    /// </param>
    /// <param name="isReset"><see langword="true" /> when triggered by SF6's "Reset to Defaults" flow.</param>
    public delegate void ValueTypeDelegate(app.Option.ValueType valueType, int value, bool isReset);

    private static readonly ConcurrentDictionary<Guid, ValueTypeDelegate> CustomValueTypes = [];

    internal static void RegisterCustomValueType(Guid id, ValueTypeDelegate action)
    {
        if (OptionTypeRegistry.TryGetOptionType(id, out var _))
            CustomValueTypes[id] = action;
        else
            throw new InvalidOperationException(
                $"Custom ValueType {id} is not registered. Call RegisterNewOptionType first.");
    }

    internal static void UnregisterCustomValueType(Guid id)
    {
        CustomValueTypes.TryRemove(id, out var _);
    }

    internal static void Clear()
    {
        CustomValueTypes.Clear();
    }

    /// <summary>
    ///     REFramework pre-hook on <see cref="app.Option.UpdatedOptionValueEvent" /> (not intended for direct calls by
    ///     mods). Skips the vanilla switch when a custom option <c>TypeId</c> is matched and invokes its handler
    ///     instead.
    /// </summary>
    /// <param name="args">
    ///     Raw VM argument span: <c>args[1]</c> is <see cref="app.Option.ValueType" />, <c>args[2]</c> is integer
    ///     value, and <c>args[3]</c> is boolean <c>isReset</c>.
    /// </param>
    /// <returns>
    ///     <see cref="PreHookResult.Skip" /> when handled by a custom delegate; otherwise
    ///     <see cref="PreHookResult.Continue" />.
    /// </returns>
    [MethodHook(typeof(app.Option), "UpdatedOptionValueEvent(app.Option.ValueType, System.Int32, System.Boolean)",
        MethodHookType.Pre)]
    public static PreHookResult OnValueTypeEvent(Span<ulong> args)
    {
        try
        {
            var valueType = (int)args[1];

            if (valueType <= OptionTypeRegistry.OptionTypeMax) return PreHookResult.Continue;

            if (!OptionTypeRegistry.TryGetOptionId(valueType, out var id)) return PreHookResult.Continue;

            if (CustomValueTypes.TryGetValue(id, out var action))
            {
                var value = (int)args[2];
                var isReset = args[3] == 1;

                action.Invoke((app.Option.ValueType)valueType, value, isReset);
                return PreHookResult.Skip;
            }
        }
        catch (Exception ex)
        {
            API.LogError($"[ValueTypeHooks] Exception in OnValueTypeEvent: {ex.Message}");
        }

        return PreHookResult.Continue;
    }
}