using SF6_Plugin_Core.UI.ReplayPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.Services;

/// <summary>
///     Tracks readiness and teardown lifecycles of SF6 managed singletons and dispatches mod initialization/cleanup
///     callbacks on the main thread.
/// </summary>
public static class GameSingletonManager
{
    private static readonly ConcurrentDictionary<GameSingletonTypes, bool> SingletonReadyStates = [];

    private static readonly ConcurrentDictionary<GameSingletonTypes, SingletonHooks> RegisteredSingletons = [];

    private static readonly GameSingletonTypes[] AllSingletonTypes = Enum.GetValues<GameSingletonTypes>();
    private static volatile bool _isReleasingTrainingManager;
    private static volatile bool _isReleasingReplayUICtrl;

    // Publicly accessible singleton instances

    /// <summary>
    ///     Gets the active <see cref="app.training.TrainingManager" /> once <c>IsInit</c> is true and
    ///     <c>_UIData._MenuData</c> is populated, or <see langword="null" /> outside Training Mode or while teardown is
    ///     in progress.
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
    ///     Gets the game's <see cref="app.UIAgentManager" /> singleton, or <see langword="null" /> before REFramework
    ///     can resolve it.
    /// </summary>
    public static app.UIAgentManager? UIAgentManager
    {
        get => field ??= API.GetManagedSingletonT<app.UIAgentManager>();
        private set;
    }

    /// <summary>
    ///     Gets the game's <see cref="app.UIPrefabManager" /> singleton, or <see langword="null" /> before REFramework
    ///     can resolve it.
    /// </summary>
    public static app.UIPrefabManager? UIPrefabManager
    {
        get => field ??= API.GetManagedSingletonT<app.UIPrefabManager>();
        private set;
    }

    /// <summary>
    ///     Gets the game's <see cref="app.network.NetworkManager" /> singleton, or <see langword="null" /> before
    ///     REFramework can resolve it.
    /// </summary>
    public static app.network.NetworkManager? NetworkManager
    {
        get => field ??= API.GetManagedSingletonT<app.network.NetworkManager>();
        private set;
    }

    /// <summary>
    ///     Gets the game's <see cref="app.bFlowManager" /> singleton, or <see langword="null" /> before REFramework can
    ///     resolve it.
    /// </summary>
    public static app.bFlowManager? BFlowManager
    {
        get => field ??= API.GetManagedSingletonT<app.bFlowManager>();
        private set;
    }

    /// <summary>
    ///     Gets RE Engine's native <see cref="REFrameworkNET.ResourceManager" /> wrapper, or <see langword="null" />
    ///     before initialization.
    /// </summary>
    public static ResourceManager? ResourceManager
    {
        get => field ??= API.GetResourceManager();
        private set;
    }

    /// <summary>
    ///     Gets the game's <see cref="app.GuiManager" /> singleton, or <see langword="null" /> before REFramework can
    ///     resolve it.
    /// </summary>
    public static app.GuiManager? GuiManager
    {
        get => field ??= API.GetManagedSingletonT<app.GuiManager>();
        private set;
    }

    /// <summary>
    ///     Gets the active <see cref="app.esports.ReplayUICtrl" /> from <see cref="GuiManager" />, or
    ///     <see langword="null" /> outside replay mode or while teardown is in progress.
    /// </summary>
    public static app.esports.ReplayUICtrl? ReplayUICtrl
    {
        get
        {
            if (_isReleasingReplayUICtrl) return null;
            if (field != null) return field;

            var ctrl = GuiManager?.Esports?.GetReplayUICtrl();
            if (ctrl != null) field = ctrl;
            return field;
        }
        private set;
    }

    /// <summary>
    ///     Gets the game's <see cref="app.OptionManager" /> singleton once <c>mInitEndFlag</c> is true and
    ///     <c>UnitLists</c> is non-null, or <see langword="null" /> before options data has loaded.
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
    ///     Gets the game's <see cref="app.ShortcutSetting" /> singleton once <c>IsLoaded</c> is true, or
    ///     <see langword="null" /> before shortcut settings are loaded or while <c>app.ShortcutSetting.Init</c> resets
    ///     them.
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

    /// <summary>
    ///     REFramework pre-update callback (not intended for direct calls by mods). Polls unready singletons once per
    ///     frame and fires queued <c>onReady</c> callbacks the first frame each becomes available.
    /// </summary>
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
            GameSingletonTypes.ReplayUICtrl => ReplayUICtrl != null,
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
    ///     Subscribes to the multi-session lifecycle of <see cref="TrainingManager" /> (entering and exiting Training
    ///     Mode). Invokes <paramref name="onReady" /> immediately if Training Mode is already active, and keeps both
    ///     callbacks registered across subsequent Training Mode entries/exits.
    /// </summary>
    /// <param name="onReady">Invoked once <c>TrainingManager.IsInit</c> and <c>_UIData._MenuData</c> are ready.</param>
    /// <param name="onRelease">
    ///     Optional cleanup invoked before <see cref="PauseMenuManager" /> and <see cref="ShortcutManager" /> clear
    ///     all remaining hooks on Training Mode exit or destruction.
    /// </param>
    public static void RegisterTrainingManager(Action onReady, Action? onRelease = null)
    {
        RegisterSingleton(GameSingletonTypes.TrainingManager, true, onReady, onRelease);
    }

    /// <summary>
    ///     Unsubscribes callbacks previously added via <see cref="RegisterTrainingManager" /> so hot-unloading a mod
    ///     does not leave stale delegate references in Core.
    /// </summary>
    /// <param name="onReady">The ready delegate passed to <see cref="RegisterTrainingManager" />.</param>
    /// <param name="onRelease">The release delegate passed to <see cref="RegisterTrainingManager" />, if any.</param>
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

    /// <summary>
    ///     REFramework pre-hook on <c>app.training.TrainingManager.Release</c> (not intended for direct calls by mods).
    ///     Tears down active Training Mode mod state, pause-menu hooks, and shortcuts before release proceeds.
    /// </summary>
    /// <param name="args">Raw VM argument span (unused).</param>
    /// <returns>Always <see cref="PreHookResult.Continue" /> so the engine finishes releasing <c>TrainingManager</c>.</returns>
    [MethodHook(typeof(app.training.TrainingManager),
        "Release", MethodHookType.Pre)]
    public static PreHookResult OnTrainingManagerReleasePre(Span<ulong> args)
    {
        ReleaseTrainingManagerState();
        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework post-hook on <c>app.training.TrainingManager.Release</c> (not intended for direct calls by mods).
    ///     Clears the in-flight release guard so future Training Mode sessions can become ready again.
    /// </summary>
    /// <param name="retval">Raw return value from the hooked method (unused).</param>
    [MethodHook(typeof(app.training.TrainingManager),
        "Release", MethodHookType.Post)]
    public static void OnTrainingManagerReleasePost(ref ulong retval)
    {
        _isReleasingTrainingManager = false;
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.training.TrainingManager.doOnDestroy</c> (not intended for direct calls by
    ///     mods). Acts as a fallback teardown path if <c>TrainingManager</c> is destroyed without <c>Release</c> firing.
    /// </summary>
    /// <param name="args">Raw VM argument span (unused).</param>
    /// <returns>Always <see cref="PreHookResult.Continue" />.</returns>
    [MethodHook(typeof(app.training.TrainingManager),
        "doOnDestroy", MethodHookType.Pre)]
    public static PreHookResult OnTrainingManagerDestroyPre(Span<ulong> args)
    {
        ReleaseTrainingManagerState();
        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework post-hook on <c>app.training.TrainingManager.doOnDestroy</c> (not intended for direct calls by
    ///     mods). Clears the in-flight release guard after destruction finishes.
    /// </summary>
    /// <param name="retval">Raw return value from the hooked method (unused).</param>
    [MethodHook(typeof(app.training.TrainingManager),
        "doOnDestroy", MethodHookType.Post)]
    public static void OnTrainingManagerDestroyPost(ref ulong retval)
    {
        _isReleasingTrainingManager = false;
    }

    /// <summary>
    ///     Registers a one-shot callback invoked when <see cref="UIAgentManager" /> first becomes non-null (or
    ///     immediately if already initialized); cleared after firing once.
    /// </summary>
    /// <param name="onReady">One-time initialization callback.</param>
    public static void RegisterUIAgentManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.UIAgentManager, false, onReady, null);
    }

    /// <summary>
    ///     Registers a one-shot callback invoked when <see cref="UIPrefabManager" /> first becomes non-null (or
    ///     immediately if already initialized); cleared after firing once.
    /// </summary>
    /// <param name="onReady">One-time initialization callback.</param>
    public static void RegisterUIPrefabManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.UIPrefabManager, false, onReady, null);
    }

    /// <summary>
    ///     Registers a one-shot callback invoked when <see cref="NetworkManager" /> first becomes non-null (or
    ///     immediately if already initialized); cleared after firing once.
    /// </summary>
    /// <param name="onReady">One-time initialization callback.</param>
    public static void RegisterNetworkManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.NetworkManager, false, onReady, null);
    }

    /// <summary>
    ///     Registers a one-shot callback invoked when <see cref="BFlowManager" /> first becomes non-null (or
    ///     immediately if already initialized); cleared after firing once.
    /// </summary>
    /// <param name="onReady">One-time initialization callback.</param>
    public static void RegisterBFlowManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.BattleFlowManager, false, onReady, null);
    }

    /// <summary>
    ///     Registers a one-shot callback invoked when <see cref="ResourceManager" /> first becomes non-null (or
    ///     immediately if already initialized); cleared after firing once.
    /// </summary>
    /// <param name="onReady">One-time initialization callback.</param>
    public static void RegisterResourceManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.ResourceManager, false, onReady, null);
    }

    /// <summary>
    ///     Registers a one-shot callback invoked when <see cref="GuiManager" /> first becomes non-null (or
    ///     immediately if already initialized); cleared after firing once.
    /// </summary>
    /// <param name="onReady">One-time initialization callback.</param>
    public static void RegisterGuiManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.GuiManager, false, onReady, null);
    }

    /// <summary>
    ///     Subscribes to <see cref="OptionManager" /> readiness and save-data reload events. Because SF6 reloads saved
    ///     options via <c>OptionManager.LoadValueData</c>, both delegates remain retained so mods can tear down and
    ///     re-apply customizations whenever save data overwrites <c>UnitLists</c>.
    /// </summary>
    /// <param name="onReady">
    ///     Invoked when <c>OptionManager.mInitEndFlag</c> and <c>UnitLists</c> first become valid (or immediately if
    ///     already ready), and again right after <paramref name="onLoadOverwrite" /> during <c>LoadValueData</c>.
    /// </param>
    /// <param name="onLoadOverwrite">
    ///     Invoked after <c>app.OptionManager.LoadValueData</c> completes so stale option UI hooks/requests can be
    ///     unloaded before <paramref name="onReady" /> re-registers them.
    /// </param>
    public static void RegisterOptionManager(Action onReady, Action onLoadOverwrite)
    {
        RegisterSingleton(GameSingletonTypes.OptionManager, true, onReady, onLoadOverwrite);
    }

    /// <summary>
    ///     Unsubscribes delegates previously registered with <see cref="RegisterOptionManager" />.
    /// </summary>
    /// <param name="onReady">The ready delegate to remove.</param>
    /// <param name="onLoadOverwrite">The overwrite/reload delegate to remove, or <see langword="null" />.</param>
    public static void UnregisterOptionManager(Action onReady, Action? onLoadOverwrite = null)
    {
        if (RegisteredSingletons.TryGetValue(GameSingletonTypes.OptionManager, out var hooks))
        {
            hooks.OnReady -= onReady;
            if (onLoadOverwrite != null) hooks.OnRelease -= onLoadOverwrite;
        }
    }

    /// <summary>
    ///     REFramework post-hook on <c>app.OptionManager.LoadValueData(app.Option.OptionSaveData)</c> (not intended for
    ///     direct calls by mods). Runs all option overwrite cleanup delegates followed by <c>onReady</c> callbacks so
    ///     custom options stay intact when save data reloads.
    /// </summary>
    /// <param name="retval">Raw return value from the hooked method (unused).</param>
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
    ///     Registers a one-shot callback invoked when <see cref="ShortcutSetting" /> becomes loaded (or immediately if
    ///     already loaded); cleared after firing once. Note that <c>ShortcutSetting.Init</c> invalidates the cached
    ///     singleton and clears <see cref="ShortcutManager" /> shortcuts on re-init.
    /// </summary>
    /// <param name="onReady">One-time initialization callback.</param>
    public static void RegisterShortcutSetting(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.ShortcutSetting, false, onReady, null);
    }

    /// <summary>
    ///     Unsubscribes a pending <see cref="RegisterShortcutSetting" /> callback before it has fired.
    /// </summary>
    /// <param name="onReady">The ready delegate to remove.</param>
    public static void UnregisterShortcutSetting(Action onReady)
    {
        if (RegisteredSingletons.TryGetValue(GameSingletonTypes.ShortcutSetting, out var hooks))
            hooks.OnReady -= onReady;
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.ShortcutSetting.Init</c> (not intended for direct calls by mods). Clears
    ///     injected shortcuts and resets cached <see cref="ShortcutSetting" /> readiness so the singleton re-evaluates
    ///     once loading finishes.
    /// </summary>
    /// <param name="args">Raw VM argument span (unused).</param>
    /// <returns>Always <see cref="PreHookResult.Continue" />.</returns>
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

    /// <summary>
    ///     Subscribes to the multi-session lifecycle of <see cref="ReplayUICtrl" /> (entering and exiting Replay mode).
    /// </summary>
    public static void RegisterReplayUICtrl(Action onReady, Action? onRelease = null)
    {
        RegisterSingleton(GameSingletonTypes.ReplayUICtrl, true, onReady, onRelease);
    }

    /// <summary>
    ///     Unsubscribes callbacks previously added via <see cref="RegisterReplayUICtrl" />.
    /// </summary>
    public static void UnregisterReplayUICtrl(Action onReady, Action? onRelease = null)
    {
        if (RegisteredSingletons.TryGetValue(GameSingletonTypes.ReplayUICtrl, out var hooks))
        {
            hooks.OnReady -= onReady;
            if (onRelease != null) hooks.OnRelease -= onRelease;
        }
    }

    private static void ReleaseReplayUICtrlState()
    {
        _isReleasingReplayUICtrl = true;

        var isReady = SingletonReadyStates.TryGetValue(GameSingletonTypes.ReplayUICtrl, out var ready) && ready;
        if (!isReady) return;

        if (RegisteredSingletons.TryGetValue(GameSingletonTypes.ReplayUICtrl, out var hooks))
            try
            {
                hooks.InvokeOnRelease();
            }
            catch (Exception ex)
            {
                API.LogError($"[GameSingletonManager] Exception in ReplayUICtrl OnRelease callback: {ex}");
            }

        ReplayPauseMenuManager.ClearReplayPauseMenuHooks();
        ReplayUICtrl = null;
        SingletonReadyStates[GameSingletonTypes.ReplayUICtrl] = false;
    }

    [MethodHook(typeof(app.GuiManager.GuiEsportsManager), "RemoveReplayUICtrl", MethodHookType.Pre)]
    public static PreHookResult OnRemoveReplayUICtrlPre(Span<ulong> args)
    {
        ReleaseReplayUICtrlState();
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.GuiManager.GuiEsportsManager), "RemoveReplayUICtrl", MethodHookType.Post)]
    public static void OnRemoveReplayUICtrlPost(ref ulong retval)
    {
        _isReleasingReplayUICtrl = false;
    }

    [MethodHook(typeof(app.esports.ReplayUICtrl), "onDestroy", MethodHookType.Pre)]
    public static PreHookResult OnReplayUICtrlDestroyPre(Span<ulong> args)
    {
        ReleaseReplayUICtrlState();
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.esports.ReplayUICtrl), "onDestroy", MethodHookType.Post)]
    public static void OnReplayUICtrlDestroyPost(ref ulong retval)
    {
        _isReleasingReplayUICtrl = false;
    }

    internal static void Clear()
    {
        _isReleasingTrainingManager = false;
        _isReleasingReplayUICtrl = false;
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
        ReplayUICtrl = null;
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
        ShortcutSetting,
        ReplayUICtrl

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