global using System;
global using System.Collections.Generic;
global using System.Runtime.InteropServices;
global using System.Linq;

global using REFrameworkNET;
global using REFrameworkNET.Attributes;
global using REFrameworkNET.Collections;
global using REFrameworkNET.Callbacks;

namespace SF6_Plugin_Core;

public static class GameSingletonRegistry
{
    // A dedicated lock object to synchronize cross-thread dictionary access
    private static readonly Lock RegistryLock = new();

    public enum GameSingletonTypes
    {
        TrainingManager,
        UIAgentManager,
        UIPrefabManager,
        NetworkManager,
        bFlowManager,
        resourceManager, // technically not a singleton, but I'd like to have a callback to it
        // Add others here later
    }

    // Publicly accessible singleton instances
    public static app.training.TrainingManager? TrainingManager { get; private set; }
    public static app.UIAgentManager? UIAgentManager { get; private set; }
    public static app.UIPrefabManager? UIPrefabManager { get; private set; }
    public static app.network.NetworkManager? NetworkManager { get; private set; }
    public static app.bFlowManager? BFlowManager { get; private set; }
    public static ResourceManager? ResourceManager { get; private set; }

    private static readonly Dictionary<GameSingletonTypes, bool> SingletonReadyStates = [];

    private class SingletonHooks
    {
        public Func<bool> CheckIfReady { get; set; } = () => false;
        public List<Action> OnReady { get; } = [];
        public List<Action> OnRelease { get; } = [];
        // Indicates whether the singleton gets reloaded multiple times during the game session (e.g., TrainingManager can be released and re-initialized)
        // if false, the action arrays will be cleared after being used
        public bool MultipleCallsAllowed { get; set; } = false;
    }

    private static readonly Dictionary<GameSingletonTypes, SingletonHooks> RegisteredSingletons = [];

    [Callback(typeof(UpdateBehavior), CallbackType.Pre)]
    private static void OnUpdateBehaviorCallback()
    {
        // Safe iteration: We lock the registry while checking/updating states
        lock (RegistryLock)
        {
            foreach (GameSingletonTypes singletonType in Enum.GetValues<GameSingletonTypes>())
            {
                if (SingletonReadyStates.TryGetValue(singletonType, out bool isReady) && isReady)
                {
                    continue; // Skip if already ready
                }

                if (RegisteredSingletons.TryGetValue(singletonType, out var hooks))
                {
                    if (hooks.CheckIfReady())
                    {
                        SingletonReadyStates[singletonType] = true;

                        // Fire all accumulated OnReady actions
                        foreach (var action in hooks.OnReady)
                        {
                            action.Invoke();
                        }

                        if (!hooks.MultipleCallsAllowed)
                        {
                            hooks.OnReady.Clear();
                        }
                    }
                }
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
        lock (RegistryLock)
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
                    hooks.OnReady.Add(onReady);
                }
            }
            else
            {
                // Otherwise, queue it for the Update loop
                hooks.OnReady.Add(onReady);
            }

            // Always queue the release callback
            if (onRelease != null)
            {
                hooks.OnRelease.Add(onRelease);
            }
        }
    }

    /// <summary>
    /// Registers the TrainingManager singleton with the GameSingletonRegistry.
    /// </summary>
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

    [MethodHook(typeof(app.training.TrainingManager), "Release", MethodHookType.Pre)]
    private static PreHookResult OnTrainingManagerReleasePre(Span<ulong> args)
    {
        // Thread-safe state cleanup
        lock (RegistryLock)
        {
            if (SingletonReadyStates.TryGetValue(GameSingletonTypes.TrainingManager, out bool isReady) && isReady)
            {
                API.LogInfo("TrainingManager released. Cleaning up mod state...");
                SingletonReadyStates[GameSingletonTypes.TrainingManager] = false;
                TrainingManager = null;

                if (RegisteredSingletons.TryGetValue(GameSingletonTypes.TrainingManager, out var hooks))
                {
                    foreach (var action in hooks.OnRelease)
                    {
                        action?.Invoke();
                    }

                    if (!hooks.MultipleCallsAllowed)
                    {
                        hooks.OnRelease.Clear();
                    }
                }
            }
        }
        return PreHookResult.Continue;
    }

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

    public static void RegisterResourceManager(Action onReady)
    {
        RegisterSingleton(GameSingletonTypes.resourceManager, () =>
        {
            var resourceManager = API.GetResourceManager();
            if (resourceManager == null) return false;

            ResourceManager = resourceManager;
            return true;
        }, multipleCallsAllowed: false, onReady, onRelease: null);
    }

    /// <summary>
    /// Call this from your [PluginExitPoint] to prevent memory leaks during hot-reloads.
    /// </summary>
    public static void Clear()
    {
        lock (RegistryLock)
        {
            SingletonReadyStates.Clear();
            RegisteredSingletons.Clear();
            UIAgentManager = null;
            UIPrefabManager = null;
            TrainingManager = null;
        }
    }
}