using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.ReplayPauseMenu.Core;

namespace SF6_Plugin_Core.UI.ReplayPauseMenu.Hooks;

/// <summary>
///     Intercepts <see cref="app.esports.ReplayUICtrl.OnDecideItem" /> to dispatch custom or hooked
///     <see cref="app.esports.UIReplayPauseMenu.EReplayFuncType" /> interactions to mod delegates.
/// </summary>
public static class ReplayDecideHooks
{
    /// <summary>
    ///     Handler invoked when a hooked or custom Replay Pause Menu item is decided or spun.
    /// </summary>
    /// <param name="replayUICtrl">The active <see cref="app.esports.ReplayUICtrl" /> instance.</param>
    /// <param name="scrollIndex">Selected option index for <c>Spin</c> items (or <c>0</c> for buttons).</param>
    /// <param name="isBattle">Whether replay takeover / battle state is active.</param>
    /// <returns>
    ///     <see cref="PreHookResult.Skip" /> to suppress vanilla handling or <see cref="PreHookResult.Continue" /> to also
    ///     run SF6's handler.
    /// </returns>
    public delegate PreHookResult ReplayDecideDelegate(
        app.esports.ReplayUICtrl replayUICtrl,
        int scrollIndex,
        bool isBattle);

    private static readonly ConcurrentDictionary<Guid, ReplayDecideDelegate> CustomDecideHandlers = [];

    internal static void RegisterDecideHandler(Guid id, ReplayDecideDelegate handler)
    {
        if (ReplayFuncTypeRegistry.TryGetFunctionType(id, out var ignored))
            CustomDecideHandlers[id] = handler;
        else
            throw new InvalidOperationException(
                $"Replay decide handler '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterDecideHandler(Guid id)
    {
        CustomDecideHandlers.TryRemove(id, out var ignored);
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.esports.ReplayUICtrl.OnDecideItem</c> (not intended for direct calls by mods).
    /// </summary>
    [MethodHook(typeof(app.esports.ReplayUICtrl), "OnDecideItem", MethodHookType.Pre)]
    public static PreHookResult OnDecideItemPre(Span<ulong> args)
    {
        if (CustomDecideHandlers.IsEmpty) return PreHookResult.Continue;
        if (args.Length < 5 || args[1] == 0) return PreHookResult.Continue;

        var funcType = (int)args[2];
        if (!ReplayFuncTypeRegistry.TryGetFunctionId(funcType, out var functionId) ||
            !CustomDecideHandlers.TryGetValue(functionId, out var handler))
            return PreHookResult.Continue;

        try
        {
            var ctrl = MemoryHelpers.GetAddressAs<app.esports.ReplayUICtrl>(args[1]);
            var scrollIndex = (int)args[3];
            var isBattle = (args[4] & 0xFF) != 0;
            return handler.Invoke(ctrl, scrollIndex, isBattle);
        }
        catch (Exception ex)
        {
            API.LogError($"[ReplayDecideHooks] Exception in OnDecideItemPre: {ex}");
            return PreHookResult.Skip;
        }
    }

    internal static void Clear()
    {
        CustomDecideHandlers.Clear();
    }
}
