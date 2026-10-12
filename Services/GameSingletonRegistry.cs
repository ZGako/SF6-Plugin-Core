namespace SF6_Plugin_Core.Services;

/// <summary>
/// Exposes the game's managed singletons and tracks their lifecycle readiness for mod integrations.
/// </summary>
public static class GameSingletonRegistry
{
    private enum GameSingletonTypes
    {
        TrainingManager,
        UIAgentManager,
        UIPrefabManager,
        NetworkManager,
        bFlowManager,
        ResourceManager, // technically not a singleton, but I'd like to have a callback to it
        GuiManager,
        // Add others here later
    }

    // Publicly accessible singleton instances
    /// <summary>
    /// Gets the current <see cref="TrainingManager"/> instance, or <see langword="null"/> when it is unavailable.
    /// </summary>
    public static app.training.TrainingManager? TrainingManager { get; private set; }

    /// <summary>
    /// Gets the current <see cref="UIAgentManager"/> instance, or <see langword="null"/> when it is unavailable.
    /// </summary>
    public static app.UIAgentManager? UIAgentManager { get; private set; }

    /// <summary>
    /// Gets the current <see cref="UIPrefabManager"/> instance, or <see langword="null"/> when it is unavailable.
    /// </summary>
    public static app.UIPrefabManager? UIPrefabManager { get; private set; }

    /// <summary>
    /// Gets the current <see cref="NetworkManager"/> instance, or <see langword="null"/> when it is unavailable.
    /// </summary>
    public static app.network.NetworkManager? NetworkManager { get; private set; }

    /// <summary>
    /// Gets the current <see cref="BFlowManager"/> instance, or <see langword="null"/> when it is unavailable.
    /// </summary>
    public static app.bFlowManager? BFlowManager { get; private set; }

    /// <summary>
    /// Gets the current <see cref="ResourceManager"/> instance, or <see langword="null"/> when it is unavailable.
    /// </summary>
    public static ResourceManager? ResourceManager { get; private set; }

    /// <summary>
    /// Gets the current <see cref="GuiManager"/> instance, or <see langword="null"/> when it is unavailable.
    /// </summary>
    public static app.GuiManager? GuiManager { get; private set; }

    private static readonly ConcurrentDictionary<GameSingletonTypes, bool> SingletonReadyStates = [];

    private class SingletonHooks
    {
        public Func<bool> CheckIfReady { get; set; } = () => false;
        public event Action? OnReady;
        public event Action? OnRelease;
        // Indicates whether the singleton gets reloaded multiple times during the game session (e.g., TrainingManager can be released and re-initialized)
        // if false, the action arrays will be cleared after being used
        public bool MultipleCallsAllowed { get; set; } = false;

        // event triggers
        public void InvokeOnReady()
        {
            var threadSafeOnReady = OnReady;
            threadSafeOnReady?.Invoke();
            if (!MultipleCallsAllowed)
            {
                OnReady = null;
            }
        }
        public void InvokeOnRelease()
        {
            var threadSafeOnRelease = OnRelease;
            threadSafeOnRelease?.Invoke();
            if (!MultipleCallsAllowed)
            {
                OnRelease = null;
            }
        }
    }

    private static readonly ConcurrentDictionary<GameSingletonTypes, SingletonHooks> RegisteredSingletons = [];

    [Callback(typeof(UpdateBehavior), CallbackType.Pre)]
    public static void OnUpdateBehaviorCallback()
    {
        foreach (var (singletonType, hooks) in RegisteredSingletons)
        {
            if (SingletonReadyStates.TryGetValue(singletonType, out bool isReady) && isReady)
            {
                continue;
            }

            if (hooks.CheckIfReady())
            {
                SingletonReadyStates[singletonType] = true;
                hooks.InvokeOnReady();
            }
        }
    }

    private static void RegisterSingleton(
         GameSingletonTypes type,
         Func<bool> checkFunc,
         bool multipleCallsAllowed,
         Action onReady,
         Action? onRelease)
    {
        if (!RegisteredSingletons.TryGetValue(type, out var hooks))
        {
            hooks = new SingletonHooks { CheckIfReady = checkFunc, MultipleCallsAllowed = multipleCallsAllowed };
            RegisteredSingletons[type] = hooks;
        }

        // If the singleton is already ready, fire the onReady callback immediately
        if (SingletonReadyStates.TryGetValue(type, out bool isReady) && isReady)
        {
            onReady.Invoke();

            // if the singleton allows multiple calls, we still add the callback to the list for future releases
            if (hooks.MultipleCallsAllowed)
            {
                hooks.OnReady += onReady;
            }
        }
        else
        {
            // Otherwise, queue it for the Update loop
            hooks.OnReady += onReady;
        }

        // Always queue the release callback
        if (onRelease != null)
        {
            hooks.OnRelease += onRelease;
        }
    }

    /// <summary>
    /// Registers a callback to run when <see cref="TrainingManager"/> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    /// <param name="onRelease">The action to invoke when the singleton is released, if provided.</param>
    public static void RegisterTrainingManager(Action onReady, Action? onRelease = null)
    {
        RegisterSingleton(GameSingletonTypes.TrainingManager, () =>
        {
            var tm = API.GetManagedSingletonT<app.training.TrainingManager>();
            if (tm == null) return false;
            if (!tm.IsInit) return false;

            TrainingManager = tm;
            return true;
        }, multipleCallsAllowed: true, onReady, onRelease);
    }

    /// <summary>
    /// Removes a callback previously registered through <see cref="RegisterTrainingManager(Action, Action?)"/>.
    /// </summary>
    /// <param name="onReady">The ready callback to remove.</param>
    /// <param name="onRelease">The release callback to remove, if provided.</param>
    public static void UnregisterTrainingManager(Action onReady, Action? onRelease = null)
    {
        if (RegisteredSingletons.TryGetValue(GameSingletonTypes.TrainingManager, out var hooks))
        {
            hooks.OnReady -= onReady;
            if (onRelease != null)
            {
                hooks.OnRelease -= onRelease;
            }
        }
    }

    [MethodHook(typeof(app.training.TrainingManager), "Release", MethodHookType.Pre)]
    public static PreHookResult OnTrainingManagerReleasePre(Span<ulong> args)
    {
        if (SingletonReadyStates.TryGetValue(GameSingletonTypes.TrainingManager, out bool isReady) && isReady)
        {
            SingletonReadyStates[GameSingletonTypes.TrainingManager] = false;
            TrainingManager = null;

            if (RegisteredSingletons.TryGetValue(GameSingletonTypes.TrainingManager, out var hooks))
            {
                hooks.InvokeOnRelease();
            }
        }
        return PreHookResult.Continue;
    }

    /// <summary>
    /// Registers a callback to run when <see cref="UIAgentManager"/> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    public static void RegisterUIAgentManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.UIAgentManager, () =>
        {
            var uiAgentManager = API.GetManagedSingletonT<app.UIAgentManager>();
            if (uiAgentManager == null) return false;

            UIAgentManager = uiAgentManager;
            return true;
        }, multipleCallsAllowed: false, onReady, onRelease: null);
    }

    /// <summary>
    /// Registers a callback to run when <see cref="UIPrefabManager"/> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    public static void RegisterUIPrefabManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.UIPrefabManager, () =>
        {
            var uiPrefabManager = API.GetManagedSingletonT<app.UIPrefabManager>();
            if (uiPrefabManager == null) return false;

            UIPrefabManager = uiPrefabManager;
            return true;
        }, multipleCallsAllowed: false, onReady, onRelease: null);
    }

    /// <summary>
    /// Registers a callback to run when <see cref="NetworkManager"/> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    public static void RegisterNetworkManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.NetworkManager, () =>
        {
            var networkManager = API.GetManagedSingletonT<app.network.NetworkManager>();
            if (networkManager == null) return false;

            NetworkManager = networkManager;
            return true;
        }, multipleCallsAllowed: false, onReady, onRelease: null);
    }

    /// <summary>
    /// Registers a callback to run when <see cref="BFlowManager"/> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    public static void RegisterBFlowManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.bFlowManager, () =>
        {
            var bFlowManager = API.GetManagedSingletonT<app.bFlowManager>();
            if (bFlowManager == null) return false;

            BFlowManager = bFlowManager;
            return true;
        }, multipleCallsAllowed: false, onReady, onRelease: null);
    }

    /// <summary>
    /// Registers a callback to run when <see cref="ResourceManager"/> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    public static void RegisterResourceManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.ResourceManager, () =>
        {
            var resourceManager = API.GetResourceManager();
            if (resourceManager == null) return false;

            ResourceManager = resourceManager;
            return true;
        }, multipleCallsAllowed: false, onReady, onRelease: null);
    }

    /// <summary>
    /// Registers a callback to run when <see cref="GuiManager"/> becomes available.
    /// </summary>
    /// <param name="onReady">The action to invoke when the singleton becomes available.</param>
    public static void RegisterGuiManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.GuiManager, () =>
        {
            var guiManager = API.GetManagedSingletonT<app.GuiManager>();
            if (guiManager == null) return false;

            GuiManager = guiManager;
            return true;
        }, multipleCallsAllowed: false, onReady, onRelease: null);
    }

    internal static void Clear()
    {
        SingletonReadyStates.Clear();
        RegisteredSingletons.Clear();
        UIAgentManager = null;
        UIPrefabManager = null;
        TrainingManager = null;
        NetworkManager = null;
        BFlowManager = null;
        ResourceManager = null;
        GuiManager = null;
    }
}