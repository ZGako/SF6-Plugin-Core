using SF6_Plugin_Core.Services;

namespace SF6_Plugin_Core.UI;

public static class BattleHudHooks
{
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

    public static void RegisterBattleHudActivationCallbacks(BattleHudStatusDelegate onActivate, Action onDeactivate)
    {
        BattleHudActivateCallback += onActivate;
        BattleHudDeactivateCallback += onDeactivate;

        var battleHudManager = TryGetBattleHudManager();
        if (battleHudManager == null) return;

        if (battleHudManager.State == app.HudManagerBase.ManagerState.ACTIVATE) onActivate?.Invoke(battleHudManager);
    }

    public static void UnregisterBattleHudCallbacks(BattleHudStatusDelegate? onSetup = null,
        BattleHudStatusDelegate? onActivate = null, Action? onDeactivate = null, Action? onUnload = null)
    {
        BattleHudSetupCallback -= onSetup;
        BattleHudActivateCallback -= onActivate;
        BattleHudDeactivateCallback -= onDeactivate;
        BattleHudUnloadCallback -= onUnload;
    }

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