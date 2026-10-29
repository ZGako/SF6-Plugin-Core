using SF6_Plugin_Core.UI.OptionsSettings.Core;

namespace SF6_Plugin_Core.UI.OptionsSettings.Hooks;

public class DisableReasonHooks
{
    public delegate bool DisableConditionDelegate();

    private static readonly ConcurrentDictionary<Guid, DisableConditionDelegate> CustomDisableConditions = new();

    [ThreadStatic] private static ulong _disableMoAddress;

    internal static void RegisterDisableCondition(Guid id, DisableConditionDelegate condition)
    {
        if (OptionTypeRegistry.TryGetOptionType(id, out var _))
            CustomDisableConditions.TryAdd(id, condition);
        else
            throw new InvalidOperationException(
                $"Custom Option {id} is not registered. Call RegisterNewOptionType first.");
    }

    internal static void UnregisterDisableCondition(Guid id)
    {
        CustomDisableConditions.TryRemove(id, out var _);
    }

    [MethodHook(typeof(app.Option),
        "GetDisableReasonType(app.Option.OptionUnitBase, app.Option.SupportModeType, System.Int32)",
        MethodHookType.Pre)]
    public static PreHookResult OnGetDisablePre(Span<ulong> args)
    {
        // Capture the target address. Matches your original snippet.
        _disableMoAddress = args[1];
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.Option),
        "GetDisableReasonType(app.Option.OptionUnitBase, app.Option.SupportModeType, System.Int32)",
        MethodHookType.Post)]
    public static void OnGetDisable(ref ulong retval)
    {
        if (_disableMoAddress == 0) return;

        var unit = ManagedObject.ToManagedObject(_disableMoAddress)?.As<app.Option.OptionUnitBase>();
        _disableMoAddress = 0; // Clear the thread static after use

        if (unit?.Setting == null) return;
        var typeId = unit.Setting.TypeId;

        if (!OptionTypeRegistry.TryGetOptionId(typeId, out var optionGuid)) return;

        if (CustomDisableConditions.TryGetValue(optionGuid, out var condition))
            // If the lambda evaluates to true, force the option to be disabled
            if (condition.Invoke())
                retval = (ulong)app.Option.DisableReasonType.Other;
    }
}