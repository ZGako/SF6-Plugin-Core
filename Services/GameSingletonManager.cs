using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.Services;

/// <summary>
///     Exposes the game's managed singletons and tracks their lifecycle readiness for mod integrations.
/// </summary>
public static class GameSingletonManager
{
    private static readonly ConcurrentDictionary<GameSingletonTypes, bool> SingletonReadyStates = [];

    private static readonly ConcurrentDictionary<GameSingletonTypes, SingletonHooks> RegisteredSingletons = [];

    private static readonly GameSingletonTypes[] AllSingletonTypes = Enum.GetValues<GameSingletonTypes>();
    private static volatile bool _isReleasingTrainingManager;

    // Publicly accessible singleton instances

    /// <summary>
    ///     Gets the current <see cref="TrainingManager" /> instance, or <see langword="null" /> when it is unavailable.
    /// </summary>
    public static app.training.TrainingManager? TrainingManager
    {
        get
        {
            if (_isReleasingTrainingManager) return null;
            if (field != null) return field;

            var tm = API.GetManagedSingletonT<app.training.TrainingManager>();
            if (tm != null && tm.IsInit && tm._UIData?._MenuData != null) field = tm;

            return field;
        }
        private set;
    }

    /// <summary>
    ///     Gets the current <see cref="UIAgentManager" /> instance, or <see langword="null" /> when it is unavailable.
    /// </summary>
    public static app.UIAgentManager? UIAgentManager
    {
        get => field ??= API.GetManagedSingletonT<app.UIAgentManager>();
        private set;
    }

    /// <summary>
    ///     Gets the current <see cref="UIPrefabManager" /> instance, or <see langword="null" /> when it is unavailable.
    /// </summary>
    public static app.UIPrefabManager? UIPrefabManager
    {
        get => field ??= API.GetManagedSingletonT<app.UIPrefabManager>();
        private set;
    }

    /// <summary>
    ///     Gets the current <see cref="NetworkManager" /> instance, or <see langword="null" /> when it is unavailable.
    /// </summary>
    public static app.network.NetworkManager? NetworkManager
    {
        get => field ??= API.GetManagedSingletonT<app.network.NetworkManager>();
        private set;
    }

    /// <summary>
    ///     Gets the current <see cref="BFlowManager" /> instance, or <see langword="null" /> when it is unavailable.
    /// </summary>
    public static app.bFlowManager? BFlowManager
    {
        get => field ??= API.GetManagedSingletonT<app.bFlowManager>();
        private set;
    }

    /// <summary>
    ///     Gets the current <see cref="ResourceManager" /> instance, or <see langword="null" /> when it is unavailable.
    /// </summary>
    public static ResourceManager? ResourceManager
    {
        get => field ??= API.GetResourceManager();
        private set;
    }

    /// <summary>
    ///     Gets the current <see cref="GuiManager" /> instance, or <see langword="null" /> when it is unavailable.
    /// </summary>
    public static app.GuiManager? GuiManager
    {
        get => field ??= API.GetManagedSingletonT<app.GuiManager>();
        private set;
    }

    /// <summary>
    ///     Gets the current <see cref="OptionManager" /> instance, or <see langword="null" /> when it is unavailable.
    /// </summary>
    public static app.OptionManager? OptionManager
    {
        get
        {
            if (field != null) return field;
            var om = API.GetManagedSingletonT<app.OptionManager>();
            if (om != null && om.mInitEndFlag && om.UnitLists != null) field = om;

            return field;
        }
        private set;
    }

    /// <summary>
    ///     Gets the current <see cref="ShortcutSetting" /> instance, or <see langword="null" /> when it is unavailable.
    /// </summary>
    public static app.ShortcutSetting? ShortcutSetting
    {
        get
        {
            if (field != null) return field;
            var ss = API.GetManagedSingletonT<app.ShortcutSetting>();
            if (ss != null && ss.IsLoaded) field = ss;

            return field;
        }
        private set;
    }

    [Callback(typeof(UpdateBehavior), CallbackType.Pre)]
    public static void OnUpdateBehaviorCallback()
    {
        foreach (var singletonType in AllSingletonTypes)
        {
            if (SingletonReadyStates.TryGetValue(singletonType, out var isReady) && isReady) continue;
            if (!IsSingletonAvailable(singletonType)) continue;

            SingletonReadyStates[singletonType] = true;

            if (singletonType == GameSingletonTypes.TrainingManager)
                LogTrainingManagerState("OnReady");

            // Callbacks are optional: only fire them if something registered
            if (RegisteredSingletons.TryGetValue(singletonType, out var hooks))
                try
                {
                    hooks.InvokeOnReady();
                }
                catch (Exception ex)
                {
                    API.LogError($"[GameSingletonManager] Exception in {singletonType} OnReady callback: {ex}");
                }
        }
    }

    // Single source of truth for the "does this singleton exist" condition
    private static bool IsSingletonAvailable(GameSingletonTypes type)
    {
        return type switch
        {
            GameSingletonTypes.TrainingManager => TrainingManager != null,
            GameSingletonTypes.UIAgentManager => UIAgentManager != null,
            GameSingletonTypes.UIPrefabManager => UIPrefabManager != null,
            GameSingletonTypes.NetworkManager => NetworkManager != null,
            GameSingletonTypes.BattleFlowManager => BFlowManager != null,
            GameSingletonTypes.ResourceManager => ResourceManager != null,
            GameSingletonTypes.GuiManager => GuiManager != null,
            GameSingletonTypes.OptionManager => OptionManager != null,
            GameSingletonTypes.ShortcutSetting => ShortcutSetting != null,
            _ => false
        };
    }

    private static void RegisterSingleton(
        GameSingletonTypes type,
        bool multipleCallsAllowed,
        Action onReady,
        Action? onRelease)
    {
        var hooks = RegisteredSingletons.GetOrAdd(type,
            _ => new SingletonHooks { MultipleCallsAllowed = multipleCallsAllowed });

        // If the singleton is already ready, fire the onReady callback immediately
        if (SingletonReadyStates.TryGetValue(type, out var isReady) && isReady)
        {
            onReady.Invoke();

            // if the singleton allows multiple calls, we still add the callback to the list for future releases
            if (hooks.MultipleCallsAllowed) hooks.OnReady += onReady;
        }
        else
        {
            // Otherwise, queue it for the Update loop
            hooks.OnReady += onReady;
        }

        // Always queue the release callback
        if (onRelease != null) hooks.OnRelease += onRelease;
    }

    /// <summary>
    ///     Registers a callback to run when <see cref="TrainingManager" /> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    /// <param name="onRelease">The action to invoke when the singleton is released, if provided.</param>
    public static void RegisterTrainingManager(Action onReady, Action? onRelease = null)
    {
        RegisterSingleton(GameSingletonTypes.TrainingManager, true, onReady, onRelease);
    }

    /// <summary>
    ///     Removes a callback previously registered through <see cref="RegisterTrainingManager(Action, Action?)" />.
    /// </summary>
    /// <param name="onReady">The ready callback to remove.</param>
    /// <param name="onRelease">The release callback to remove, if provided.</param>
    public static void UnregisterTrainingManager(Action onReady, Action? onRelease = null)
    {
        if (RegisteredSingletons.TryGetValue(GameSingletonTypes.TrainingManager, out var hooks))
        {
            hooks.OnReady -= onReady;
            if (onRelease != null) hooks.OnRelease -= onRelease;
        }
    }

    private static void LogTrainingManagerState(string phase, bool? wasReady = null)
    {
        try
        {
            var tmMo = API.GetManagedSingleton("app.training.TrainingManager");
            var tmAddr = tmMo?.GetAddress() ?? 0;
            if (tmMo == null || tmAddr == 0 || !ManagedObject.IsManagedObject(tmAddr))
            {
                API.LogInfo(
                    $"[GameSingletonManager] TrainingManager {phase}: singleton=null (wasReady={wasReady?.ToString() ?? "n/a"})");
                return;
            }

            var tm = tmMo.As<app.training.TrainingManager>();
            if (tm == null)
            {
                API.LogInfo(
                    $"[GameSingletonManager] TrainingManager {phase}: addr=0x{tmAddr:X}, proxy=null (wasReady={wasReady?.ToString() ?? "n/a"})");
                return;
            }

            var uiDataMo = app.training.TrainingManager.REFType?.GetField("_UIData")
                ?.GetDataBoxed(tmAddr, false) as ManagedObject;
            var uiDataAddr = uiDataMo?.GetAddress() ?? 0;
            var isValidUiData = uiDataAddr != 0 && ManagedObject.IsManagedObject(uiDataAddr);
            var menuDataMo = isValidUiData
                ? app.training.TrainingPauseMenuUserData.REFType?.GetField("_MenuData")
                    ?.GetDataBoxed(uiDataAddr, false) as ManagedObject
                : null;
            var menuDataAddr = menuDataMo?.GetAddress() ?? 0;

            API.LogInfo(
                $"[GameSingletonManager] TrainingManager {phase}: wasReady={wasReady?.ToString() ?? "n/a"}, " +
                $"IsInit={tm.IsInit}, IsSetuped={tm.IsSetuped}, IsLoaded={tm.IsLoaded}, " +
                $"_IsReqRelease={tm._IsReqRelease}, _UIData=0x{uiDataAddr:X}, _MenuData=0x{menuDataAddr:X}");
        }
        catch (Exception ex)
        {
            API.LogWarning($"[GameSingletonManager] Failed logging TrainingManager state ({phase}): {ex.Message}");
        }
    }

    private static void ReleaseTrainingManagerState()
    {
        _isReleasingTrainingManager = true;

        var isReady = SingletonReadyStates.TryGetValue(GameSingletonTypes.TrainingManager, out var ready) && ready;
        LogTrainingManagerState("ReleasePre", isReady);

        if (isReady)
        {
            if (RegisteredSingletons.TryGetValue(GameSingletonTypes.TrainingManager, out var hooks))
                hooks.InvokeOnRelease();

            PauseMenuManager.ClearTrainingPauseMenuHooks();
            ShortcutManager.ClearShortcuts();

            TrainingManager = null;
            SingletonReadyStates[GameSingletonTypes.TrainingManager] = false;
        }
    }

    [MethodHook(typeof(app.training.TrainingManager),
        "Release", MethodHookType.Pre)]
    public static PreHookResult OnTrainingManagerReleasePre(Span<ulong> args)
    {
        ReleaseTrainingManagerState();
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.TrainingManager),
        "Release", MethodHookType.Post)]
    public static void OnTrainingManagerReleasePost(ref ulong retval)
    {
        _isReleasingTrainingManager = false;
    }

    [MethodHook(typeof(app.training.TrainingManager),
        "doOnDestroy", MethodHookType.Pre)]
    public static PreHookResult OnTrainingManagerDestroyPre(Span<ulong> args)
    {
        ReleaseTrainingManagerState();
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.TrainingManager),
        "doOnDestroy", MethodHookType.Post)]
    public static void OnTrainingManagerDestroyPost(ref ulong retval)
    {
        _isReleasingTrainingManager = false;
    }

    /// <summary>
    ///     Registers a callback to run when <see cref="UIAgentManager" /> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    public static void RegisterUIAgentManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.UIAgentManager, false, onReady, null);
    }

    /// <summary>
    ///     Registers a callback to run when <see cref="UIPrefabManager" /> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    public static void RegisterUIPrefabManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.UIPrefabManager, false, onReady, null);
    }

    /// <summary>
    ///     Registers a callback to run when <see cref="NetworkManager" /> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    public static void RegisterNetworkManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.NetworkManager, false, onReady, null);
    }

    /// <summary>
    ///     Registers a callback to run when <see cref="BFlowManager" /> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    public static void RegisterBFlowManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.BattleFlowManager, false, onReady, null);
    }

    /// <summary>
    ///     Registers a callback to run when <see cref="ResourceManager" /> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    public static void RegisterResourceManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.ResourceManager, false, onReady, null);
    }

    /// <summary>
    ///     Registers a callback to run when <see cref="GuiManager" /> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    public static void RegisterGuiManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.GuiManager, false, onReady, null);
    }

    /// <summary>
    ///     Registers a callback to run when <see cref="OptionManager" /> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    /// <param name="onLoadOverwrite">
    ///     Action to invoke to unload the settings (after which the onReady function will be called
    ///     again)
    /// </param>
    public static void RegisterOptionManager(Action onReady, Action onLoadOverwrite)
    {
        RegisterSingleton(GameSingletonTypes.OptionManager, true, onReady, onLoadOverwrite);
    }

    public static void UnregisterOptionManager(Action onReady, Action? onLoadOverwrite = null)
    {
        if (RegisteredSingletons.TryGetValue(GameSingletonTypes.OptionManager, out var hooks))
        {
            hooks.OnReady -= onReady;
            if (onLoadOverwrite != null) hooks.OnRelease -= onLoadOverwrite;
        }
    }

    [MethodHook(typeof(app.OptionManager), "LoadValueData(app.Option.OptionSaveData)", MethodHookType.Post)]
    public static void OnLoadValueData(ref ulong retval)
    {
        if (SingletonReadyStates.TryGetValue(GameSingletonTypes.OptionManager, out var isReady) && isReady)
            if (RegisteredSingletons.TryGetValue(GameSingletonTypes.OptionManager, out var hooks))
            {
                try
                {
                    hooks.InvokeOnRelease();
                }
                catch (Exception ex)
                {
                    API.LogError($"[GameSingletonManager] Exception in OptionManager OnRelease callback: {ex}");
                }

                try
                {
                    hooks.InvokeOnReady();
                }
                catch (Exception ex)
                {
                    API.LogError($"[GameSingletonManager] Exception in OptionManager OnReady callback: {ex}");
                }
            }
    }

    /// <summary>
    ///     Registers a callback to run when <see cref="ShortcutSetting" /> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    public static void RegisterShortcutSetting(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.ShortcutSetting, false, onReady, null);
    }

    public static void UnregisterShortcutSetting(Action onReady)
    {
        if (RegisteredSingletons.TryGetValue(GameSingletonTypes.ShortcutSetting, out var hooks))
            hooks.OnReady -= onReady;
    }

    [MethodHook(typeof(app.ShortcutSetting), "Init", MethodHookType.Pre)]
    public static PreHookResult OnShortcutInitReleasePre(Span<ulong> args)
    {
        if (SingletonReadyStates.TryGetValue(GameSingletonTypes.ShortcutSetting, out var isReady) && isReady)
        {
            ShortcutManager.ClearShortcuts();

            ShortcutSetting = null;
            SingletonReadyStates[GameSingletonTypes.ShortcutSetting] = false;
        }


        return PreHookResult.Continue;
    }

    internal static void Clear()
    {
        _isReleasingTrainingManager = false;
        SingletonReadyStates.Clear();
        RegisteredSingletons.Clear();
        UIAgentManager = null;
        UIPrefabManager = null;
        TrainingManager = null;
        NetworkManager = null;
        BFlowManager = null;
        ResourceManager = null;
        GuiManager = null;
        OptionManager = null;
        ShortcutSetting = null;
    }

    private enum GameSingletonTypes
    {
        TrainingManager,
        UIAgentManager,
        UIPrefabManager,
        NetworkManager,
        BattleFlowManager,
        ResourceManager, // technically not a singleton, but I'd like to have a callback to it
        GuiManager,
        OptionManager,
        ShortcutSetting

        // Add others here later
    }

    private class SingletonHooks
    {
        // Indicates whether the singleton gets reloaded multiple times during the game session (e.g., TrainingManager can be released and re-initialized)
        // if false, the action arrays will be cleared after being used
        public bool MultipleCallsAllowed { get; init; }
        public event Action? OnReady;
        public event Action? OnRelease;

        // event triggers
        public void InvokeOnReady()
        {
            var threadSafeOnReady = OnReady;
            threadSafeOnReady?.Invoke();
            if (!MultipleCallsAllowed) OnReady = null;
        }

        public void InvokeOnRelease()
        {
            var threadSafeOnRelease = OnRelease;
            threadSafeOnRelease?.Invoke();
            if (!MultipleCallsAllowed) OnRelease = null;
        }
    }
}