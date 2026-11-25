using SF6_Plugin_Core.UI.OptionsSettings.Core;

namespace SF6_Plugin_Core.UI.OptionsSettings.Hooks;

public class DisableReasonHooks
{
    public delegate bool DisableConditionDelegate();

    private static readonly ConcurrentDictionary<Guid, DisableConditionDelegate> CustomDisableConditions = new();

    [ThreadStatic] private static Stack<ulong>? s_disableMoAddresses;

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

    internal static void Clear()
    {
        CustomDisableConditions.Clear();
    }

    [MethodHook(typeof(app.Option),
        "GetDisableReasonType(app.Option.OptionUnitBase, app.Option.SupportModeType, System.Int32)",
        MethodHookType.Pre)]
    public static PreHookResult OnGetDisablePre(Span<ulong> args)
    {
        s_disableMoAddresses ??= new Stack<ulong>();
        s_disableMoAddresses.Push(args.Length > 1 ? args[1] : 0);
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.Option),
        "GetDisableReasonType(app.Option.OptionUnitBase, app.Option.SupportModeType, System.Int32)",
        MethodHookType.Post)]
    public static void OnGetDisable(ref ulong retval)
    {
        if (s_disableMoAddresses == null || !s_disableMoAddresses.TryPop(out var addr) || addr == 0)
            return;

        try
        {
            if (!ManagedObject.IsManagedObject(addr)) return;

            var unit = ManagedObject.ToManagedObject(addr)?.As<app.Option.OptionUnitBase>();
            if (unit?.Setting == null) return;

            var typeId = unit.Setting.TypeId;
            if (!OptionTypeRegistry.TryGetOptionId(typeId, out var optionGuid)) return;

            if (CustomDisableConditions.TryGetValue(optionGuid, out var condition))
                // If the lambda evaluates to true, force the option to be disabled
                if (condition.Invoke())
                    retval = (ulong)app.Option.DisableReasonType.Other;
        }
        catch (Exception ex)
        {
            API.LogError($"[DisableReasonHooks] Exception in OnGetDisable: {ex.Message}");
        }
    }
}