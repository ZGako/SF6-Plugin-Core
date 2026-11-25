using SF6_Plugin_Core.Services;

namespace SF6_Plugin_Core.UI;

/// <summary>
///     Dispatches SF6 battle HUD lifecycle events (<see cref="app.BattleHudManager" />) to registered mod callbacks.
/// </summary>
public static class BattleHudHooks
{
    /// <summary>
    ///     Callback invoked with the active <see cref="app.BattleHudManager" /> when the battle HUD reaches setup or
    ///     activation.
    /// </summary>
    /// <param name="battleHudManager">The active <see cref="app.BattleHudManager" /> resolved from <c>GuiManager.Hud</c>.</param>
    public delegate void BattleHudStatusDelegate(app.BattleHudManager battleHudManager);

    private static event BattleHudStatusDelegate? BattleHudSetupCallback;
    private static event BattleHudStatusDelegate? BattleHudActivateCallback;
    private static event Action? BattleHudDeactivateCallback;
    private static event Action? BattleHudUnloadCallback;

    private static app.BattleHudManager? TryGetBattleHudManager()
    {
        var list = GameSingletonManager.GuiManager?.Hud?.mHudManagerList;
        if (list == null || list.Count == 0) return null;

        return ManagedProxy<app.BattleHudManager>.Create(list[0]);
    }

    /// <summary>
    ///     Registers callbacks for battle HUD setup and unload transitions. If the HUD is already in
    ///     <see cref="app.HudManagerBase.ManagerState.LOADING" />, <see cref="app.HudManagerBase.ManagerState.ACTIVATE" />,
    ///     or <see cref="app.HudManagerBase.ManagerState.DEACTIVATE" />, <paramref name="onSetup" /> is invoked
    ///     immediately.
    /// </summary>
    /// <param name="onSetup">Invoked once setup finishes (or immediately if already set up).</param>
    /// <param name="onUnload">Invoked when <see cref="app.BattleHudManager.ManagerEvent_UnloadHud" /> fires.</param>
    public static void RegisterBattleHudSetupCallbacks(BattleHudStatusDelegate onSetup, Action onUnload)
    {
        BattleHudSetupCallback += onSetup;
        BattleHudUnloadCallback += onUnload;

        var battleHudManager = TryGetBattleHudManager();
        if (battleHudManager == null) return;

        if (battleHudManager.State == app.HudManagerBase.ManagerState.LOADING ||
            battleHudManager.State == app.HudManagerBase.ManagerState.ACTIVATE ||
            battleHudManager.State == app.HudManagerBase.ManagerState.DEACTIVATE)
            onSetup?.Invoke(battleHudManager);
    }

    /// <summary>
    ///     Registers callbacks for battle HUD activation and deactivation (e.g., match start vs. pause/cutscenes).
    ///     Invokes <paramref name="onActivate" /> immediately if the HUD is currently in
    ///     <see cref="app.HudManagerBase.ManagerState.ACTIVATE" />.
    /// </summary>
    /// <param name="onActivate">Invoked when the HUD becomes active.</param>
    /// <param name="onDeactivate">Invoked when <see cref="app.BattleHudManager.HudEvent_Deactivate" /> fires.</param>
    public static void RegisterBattleHudActivationCallbacks(BattleHudStatusDelegate onActivate, Action onDeactivate)
    {
        BattleHudActivateCallback += onActivate;
        BattleHudDeactivateCallback += onDeactivate;

        var battleHudManager = TryGetBattleHudManager();
        if (battleHudManager == null) return;

        if (battleHudManager.State == app.HudManagerBase.ManagerState.ACTIVATE) onActivate?.Invoke(battleHudManager);
    }

    /// <summary>
    ///     Unsubscribes any combination of previously registered battle HUD lifecycle delegates.
    /// </summary>
    /// <param name="onSetup">Setup callback to remove, or <see langword="null" />.</param>
    /// <param name="onActivate">Activation callback to remove, or <see langword="null" />.</param>
    /// <param name="onDeactivate">Deactivation callback to remove, or <see langword="null" />.</param>
    /// <param name="onUnload">Unload callback to remove, or <see langword="null" />.</param>
    public static void UnregisterBattleHudCallbacks(BattleHudStatusDelegate? onSetup = null,
        BattleHudStatusDelegate? onActivate = null, Action? onDeactivate = null, Action? onUnload = null)
    {
        BattleHudSetupCallback -= onSetup;
        BattleHudActivateCallback -= onActivate;
        BattleHudDeactivateCallback -= onDeactivate;
        BattleHudUnloadCallback -= onUnload;
    }

    /// <summary>
    ///     REFramework post-hook on <see cref="app.BattleHudManager.ManagerEvent_SetupHudEnd" /> (not intended for
    ///     direct calls by mods). Forwards the active manager to setup subscribers once its state is no longer
    ///     <see cref="app.HudManagerBase.ManagerState.INVALID" />.
    /// </summary>
    /// <param name="retval">Raw return value from the hooked engine method (unused).</param>
    [MethodHook(typeof(app.BattleHudManager), nameof(app.BattleHudManager.ManagerEvent_SetupHudEnd),
        MethodHookType.Post)]
    public static void OnBattleHudSetupEnd(ref ulong retval)
    {
        var callback = BattleHudSetupCallback;
        if (callback == null) return; // No callbacks registered, exit early

        try
        {
            var battleHudManager = TryGetBattleHudManager();
            if (battleHudManager == null) return;

            if (battleHudManager.State ==
                app.HudManagerBase.ManagerState.INVALID) return; // BattleHudManager is not ready yet, exit early

            callback.Invoke(battleHudManager);
        }
        catch (Exception ex)
        {
            API.LogError($"[BattleHudHooks] Exception in OnBattleHudSetupEnd: {ex}");
        }
    }

    /// <summary>
    ///     REFramework post-hook on <see cref="app.BattleHudManager.HudEvent_Activate" /> (not intended for direct calls
    ///     by mods). Notifies activation subscribers when the HUD manager is in a valid state.
    /// </summary>
    /// <param name="retval">Raw return value from the hooked engine method (unused).</param>
    [MethodHook(typeof(app.BattleHudManager), nameof(app.BattleHudManager.HudEvent_Activate), MethodHookType.Post)]
    public static void OnBattleHudActivate(ref ulong retval)
    {
        var callback = BattleHudActivateCallback;
        if (callback == null) return; // No callbacks registered, exit early

        try
        {
            var battleHudManager = TryGetBattleHudManager();
            if (battleHudManager == null) return;

            if (battleHudManager.State ==
                app.HudManagerBase.ManagerState.INVALID) return; // BattleHudManager is not ready yet, exit early

            callback.Invoke(battleHudManager);
        }
        catch (Exception ex)
        {
            API.LogError($"[BattleHudHooks] Exception in OnBattleHudActivate: {ex}");
        }
    }

    /// <summary>
    ///     REFramework post-hook on <see cref="app.BattleHudManager.HudEvent_Deactivate" /> (not intended for direct
    ///     calls by mods). Notifies deactivation subscribers.
    /// </summary>
    /// <param name="retval">Raw return value from the hooked engine method (unused).</param>
    [MethodHook(typeof(app.BattleHudManager), nameof(app.BattleHudManager.HudEvent_Deactivate), MethodHookType.Post)]
    public static void OnBattleHudDeactivate(ref ulong retval)
    {
        var callback = BattleHudDeactivateCallback;
        if (callback == null) return; // No callbacks registered, exit early

        try
        {
            callback.Invoke();
        }
        catch (Exception ex)
        {
            API.LogError($"[BattleHudHooks] Exception in OnBattleHudDeactivate: {ex}");
        }
    }

    /// <summary>
    ///     REFramework post-hook on <see cref="app.BattleHudManager.ManagerEvent_UnloadHud" /> (not intended for direct
    ///     calls by mods). Notifies unload subscribers so HUD-owned elements can be torn down.
    /// </summary>
    /// <param name="retval">Raw return value from the hooked engine method (unused).</param>
    [MethodHook(typeof(app.BattleHudManager), nameof(app.BattleHudManager.ManagerEvent_UnloadHud), MethodHookType.Post)]
    public static void OnBattleHudUnload(ref ulong retval)
    {
        var callback = BattleHudUnloadCallback;
        if (callback == null) return; // No callbacks registered, continue as normal

        try
        {
            callback.Invoke();
        }
        catch (Exception ex)
        {
            API.LogError($"[BattleHudHooks] Exception in OnBattleHudUnload: {ex}");
        }
    }

    internal static void ClearAllRegisteredBattleHudCallbacks()
    {
        BattleHudSetupCallback = null;
        BattleHudActivateCallback = null;
        BattleHudDeactivateCallback = null;
        BattleHudUnloadCallback = null;
    }
}