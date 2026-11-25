using SF6_Plugin_Core.UI.OptionsSettings.Core;

namespace SF6_Plugin_Core.UI.OptionsSettings.Hooks;

/// <summary>
///     Intercepts <see cref="app.Option.GetDisableReasonType" /> so custom Options entries can dynamically report
///     themselves disabled (<see cref="app.Option.DisableReasonType.Other" />).
/// </summary>
public class DisableReasonHooks
{
    /// <summary>
    ///     Predicate evaluated whenever SF6 queries whether a custom option is disabled. Returning <see langword="true" />
    ///     forces the option to render disabled with <see cref="app.Option.DisableReasonType.Other" />.
    /// </summary>
    /// <returns><see langword="true" /> to lock/gray out the option row; <see langword="false" /> to preserve normal availability.</returns>
    public delegate bool DisableConditionDelegate();

    private static readonly ConcurrentDictionary<Guid, DisableConditionDelegate> CustomDisableConditions = new();

    [ThreadStatic] private static Stack<ulong>? s_disableMoAddresses;

    internal static void RegisterDisableCondition(Guid id, DisableConditionDelegate condition)
    {
        if (OptionTypeRegistry.TryGetOptionType(id, out var _))
            CustomDisableConditions[id] = condition;
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

    /// <summary>
    ///     REFramework pre-hook on <see cref="app.Option.GetDisableReasonType" /> (not intended for direct calls by
    ///     mods). Captures the target <see cref="app.Option.OptionUnitBase" /> address on a thread-local stack for
    ///     inspection in <see cref="OnGetDisable" />.
    /// </summary>
    /// <param name="args">Raw VM argument span where <c>args[1]</c> is the <c>app.Option.OptionUnitBase</c> argument.</param>
    /// <returns>Always <see cref="PreHookResult.Continue" /> so the original check runs first.</returns>
    [MethodHook(typeof(app.Option),
        "GetDisableReasonType(app.Option.OptionUnitBase, app.Option.SupportModeType, System.Int32)",
        MethodHookType.Pre)]
    public static PreHookResult OnGetDisablePre(Span<ulong> args)
    {
        s_disableMoAddresses ??= new Stack<ulong>();
        s_disableMoAddresses.Push(args.Length > 1 ? args[1] : 0);
        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework post-hook on <see cref="app.Option.GetDisableReasonType" /> (not intended for direct calls by
    ///     mods). Overrides <paramref name="retval" /> to <see cref="app.Option.DisableReasonType.Other" /> when the
    ///     registered predicate for the option's <c>Setting.TypeId</c> returns <see langword="true" />.
    /// </summary>
    /// <param name="retval">Raw enum return value (<see cref="app.Option.DisableReasonType" />) from the engine call.</param>
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