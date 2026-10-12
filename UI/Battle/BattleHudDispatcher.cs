

using SF6_Plugin_Core.Services;

namespace SF6_Plugin_Core.UI;

public static class BattleHudDispatcher
{
    public delegate void BattleHudStatusDelegate(app.BattleHudManager battleHudManager);

    private static event BattleHudStatusDelegate? BattleHudSetupCallback;
    private static event BattleHudStatusDelegate? BattleHudActivateCallback;
    private static event Action? BattleHudDeactivateCallback;
    private static event Action? BattleHudUnloadCallback;

    public static void RegisterBattleHudSetupCallbacks(BattleHudStatusDelegate onSetup, Action onUnload)
    {
        BattleHudSetupCallback += onSetup;
        BattleHudUnloadCallback += onUnload;

        if (GameSingletonRegistry.GuiManager == null)
        {
            return; // GuiManager is not ready yet, exit early
        }

        var battleHudAsBase = GameSingletonRegistry.GuiManager.Hud.mHudManagerList[0];
        var battleHudManager = ManagedProxy<app.BattleHudManager>.Create(battleHudAsBase);

        // if casting failed
        if (battleHudManager == null)
        {
            return;
        }

        if (battleHudManager.State == app.HudManagerBase.ManagerState.LOADING ||
            battleHudManager.State == app.HudManagerBase.ManagerState.ACTIVATE ||
            battleHudManager.State == app.HudManagerBase.ManagerState.DEACTIVATE)
        {
            onSetup?.Invoke(battleHudManager);
        }
    }

    public static void RegisterBattleHudActivationCallbacks(BattleHudStatusDelegate onActivate, Action onDeactivate)
    {
        BattleHudActivateCallback += onActivate;
        BattleHudDeactivateCallback += onDeactivate;

        if (GameSingletonRegistry.GuiManager == null)
        {
            return; // GuiManager is not ready yet, exit early
        }

        var battleHudAsBase = GameSingletonRegistry.GuiManager.Hud.mHudManagerList[0];
        var battleHudManager = ManagedProxy<app.BattleHudManager>.Create(battleHudAsBase);

        // if casting failed
        if (battleHudManager == null)
        {
            return;
        }

        if (battleHudManager.State == app.HudManagerBase.ManagerState.ACTIVATE)
        {
            onActivate?.Invoke(battleHudManager);
        }
    }

    public static void UnregisterBattleHudCallbacks(BattleHudStatusDelegate? onSetup = null, BattleHudStatusDelegate? onActivate = null, Action? onDeactivate = null, Action? onUnload = null)
    {
        BattleHudSetupCallback -= onSetup;
        BattleHudActivateCallback -= onActivate;
        BattleHudDeactivateCallback -= onDeactivate;
        BattleHudUnloadCallback -= onUnload;
    }

    [MethodHook(typeof(app.BattleHudManager), nameof(app.BattleHudManager.ManagerEvent_SetupHudEnd), MethodHookType.Post)]
    public static void OnBattleHudSetupEndTest(ref ulong retval)
    {
        if (BattleHudSetupCallback == null)
        {
            return; // No callbacks registered, exit early
        }

        if (GameSingletonRegistry.GuiManager == null)
        {
            return; // GuiManager is not ready yet, exit early
        }

        var battleHudAsBase = GameSingletonRegistry.GuiManager.Hud.mHudManagerList[0];
        var battleHudManager = ManagedProxy<app.BattleHudManager>.Create(battleHudAsBase);

        // if casting failed
        if (battleHudManager == null)
        {
            return;
        }

        if (battleHudManager.State == app.HudManagerBase.ManagerState.INVALID)
        {
            return; // BattleHudManager is not ready yet, exit early
        }
        BattleHudSetupCallback?.Invoke(battleHudManager);
    }


    [MethodHook(typeof(app.BattleHudManager), nameof(app.BattleHudManager.HudEvent_Activate), MethodHookType.Post)]
    public static void OnBattleHudSetupEnd(ref ulong retval)
    {
        if (BattleHudActivateCallback == null)
        {
            return; // No callbacks registered, exit early
        }

        if (GameSingletonRegistry.GuiManager == null)
        {
            return; // GuiManager is not ready yet, exit early
        }

        var battleHudAsBase = GameSingletonRegistry.GuiManager.Hud.mHudManagerList[0];
        var battleHudManager = ManagedProxy<app.BattleHudManager>.Create(battleHudAsBase);

        // if casting failed
        if (battleHudManager == null)
        {
            return;
        }

        if (battleHudManager.State == app.HudManagerBase.ManagerState.INVALID)
        {
            return; // BattleHudManager is not ready yet, exit early
        }

        BattleHudActivateCallback?.Invoke(battleHudManager);
    }

    [MethodHook(typeof(app.BattleHudManager), nameof(app.BattleHudManager.HudEvent_Deactivate), MethodHookType.Post)]
    public static void OnBattleHudDeactivate(ref ulong retval)
    {
        if (BattleHudDeactivateCallback == null)
        {
            return; // No callbacks registered, exit early
        }

        BattleHudDeactivateCallback.Invoke();
    }

    [MethodHook(typeof(app.BattleHudManager), nameof(app.BattleHudManager.ManagerEvent_UnloadHud), MethodHookType.Post)]
    public static void OnBattleHudUnload(ref ulong retval)
    {
        if (BattleHudUnloadCallback == null)
        {
            return; // No callbacks registered, continue as normal
        }

        BattleHudUnloadCallback.Invoke();

        // var battleHudManager = MemoryHelpers.GetAddressAs<app.BattleHudManager>(args[1]);

        return;
    }

    internal static void ClearAllRegisteredBattleHudCallbacks()
    {
        BattleHudSetupCallback = null;
        BattleHudActivateCallback = null;
        BattleHudDeactivateCallback = null;
        BattleHudUnloadCallback = null;
    }

}