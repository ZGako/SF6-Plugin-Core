namespace SF6_Plugin_Core.Services;

/// <summary>
/// Dispatches flow transition notifications to registered callbacks and game-mode handlers.
/// </summary>
public static class FlowTransitionDispatcher
{
    /// <summary>
    /// Represents a callback invoked when a flow transition occurs.
    /// </summary>
    /// <param name="flowWork">The flow work being transitioned.</param>
    public delegate void FlowTransitionDelegate(app.bFlowManager.FlowWork flowWork);

    private static event FlowTransitionDelegate? FlowTransitionCallback;

    // OnEntry, OnExit
    private readonly static ConcurrentDictionary<app.EGameMode, (Action?, Action?)> GameModeCallbacks = [];

    private static app.EGameMode _prevGameMode = app.EGameMode.NONE;

    /// <summary>
    /// Registers a callback to run for every flow transition. It also invokes the callback immediately with the current flow work if available.
    /// </summary>
    /// <param name="flowTransitionDelegate">The callback to invoke with the current flow work.</param>
    /// <remarks> The callback will be invoked every time, make sure it handles entering and exiting of the bFlow properly on its own. </remarks>
    public static void RegisterFlowTransitionCallback(FlowTransitionDelegate flowTransitionDelegate)
    {
        FlowTransitionCallback += flowTransitionDelegate;

        if (GameSingletonRegistry.BFlowManager == null)
        {
            return; // BFlowManager is not ready yet, exit early
        }
        // invoke to make sure the callback is aware of the current flow work
        flowTransitionDelegate?.Invoke(GameSingletonRegistry.BFlowManager.m_flow_work);
    }

    /// <summary>
    /// Removes a callback previously registered through <see cref="RegisterFlowTransitionCallback(FlowTransitionDelegate)"/>.
    /// </summary>
    /// <param name="flowTransitionDelegate">The callback to unregister.</param>
    public static void UnregisterFlowTransitionCallback(FlowTransitionDelegate flowTransitionDelegate)
    {
        FlowTransitionCallback -= flowTransitionDelegate;
    }

    /// <summary>
    /// Registers a callback to run when a flow transition occurs for the specified game mode. The callback will be invoked immediately if the current flow map matches the specified game mode.
    /// </summary>
    /// <param name="gameMode">The game mode associated with the transition.</param>
    /// <param name="onEntryCallback">The callback to invoke when entering the state.</param>
    /// <param name="onExitCallback">The callback to invoke when exiting the state.</param>
    /// <remarks> The GameMode is only valid if the battleScene is eBattleMain (i.e. the actual battle and not a cutscene or menu) </remarks>
    public static void RegisterGameModeFlowTransition(app.EGameMode gameMode, Action onEntryCallback, Action onExitCallback)
    {
        GameModeCallbacks.AddOrUpdate(
            gameMode,
            (onEntryCallback, onExitCallback),
            (_, existing) => (existing.Item1 + onEntryCallback, existing.Item2 + onExitCallback)
        );


        if (GameSingletonRegistry.BFlowManager == null)
        {
            return; // BFlowManager is not ready yet, exit early
        }
        var flowWork = GameSingletonRegistry.BFlowManager.m_flow_work;
        if (gameMode == GetCurrentGameMode(flowWork))
        {
            onEntryCallback?.Invoke();
            _prevGameMode = gameMode;
        }
    }

    /// <summary>
    /// Removes a game-mode callback previously registered through <see cref="RegisterGameModeFlowTransition(app.EGameMode, Action)"/>.
    /// </summary>
    /// <param name="gameMode">The game mode whose callback is being removed.</param>
    /// <param name="onEntryCallback">The callback to invoke when entering the state.</param>
    /// <param name="onExitCallback">The callback to invoke when exiting the state.</param>
    public static void UnregisterGameModeFlowTransition(app.EGameMode gameMode, Action onEntryCallback, Action onExitCallback)
    {
        if (GameModeCallbacks.TryGetValue(gameMode, out var existingCallbacks))
        {
            var updatedCallbacks = (existingCallbacks.Item1 - onEntryCallback, existingCallbacks.Item2 - onExitCallback);

            if (updatedCallbacks.Item1 == null && updatedCallbacks.Item2 == null)
            {
                GameModeCallbacks.TryRemove(gameMode, out var _);
            }
            else
            {
                GameModeCallbacks[gameMode] = updatedCallbacks;
            }
        }

    }

    private static app.EGameMode GetCurrentGameMode(app.bFlowManager.FlowWork flowWork)
    {

        var sceneIdx = flowWork.SceneId;

        if (sceneIdx != app.constant.scn.Index.eBattleMain)
        {
            return app.EGameMode.NONE; // Not in battle scene, exit early
        }

        var flowMap = flowWork.FlowMap;

        if (flowMap is not IObject flowObj || !flowObj.GetTypeDefinition().IsDerivedFrom(app.battle.BattleFlowMap.REFType))
        {
            return app.EGameMode.NONE; // FlowMap is not a BattleFlowMap, exit early
        }

        var battleFlowMap = ManagedProxy<app.battle.BattleFlowMap>.Create(flowMap);

        if (battleFlowMap == null)
        {
            return app.EGameMode.NONE; // Failed to create ManagedProxy for BattleFlowMap, exit early
        }

        return battleFlowMap.m_desc.Rule.GameMode;
    }


    [MethodHook(typeof(app.bFlowManager.FlowWork), nameof(app.bFlowManager.FlowWork.transition), MethodHookType.Post)]
    public static void OnFlowTransition(ref ulong retval)
    {
        if (FlowTransitionCallback == null && GameModeCallbacks.IsEmpty)
        {
            return; // No callbacks registered, exit early
        }

        var flowWork = GameSingletonRegistry.BFlowManager!.m_flow_work;

        var threadSafeCopy = FlowTransitionCallback; // Make a thread-safe copy of the delegate
        threadSafeCopy?.Invoke(flowWork);

        var gameMode = GetCurrentGameMode(flowWork);

        if (_prevGameMode != gameMode)
        {
            // iterate through the non-current game modes from the previous game mode 
            if (GameModeCallbacks.TryGetValue(_prevGameMode, out var prevGameModeCallbacks))
            {
                prevGameModeCallbacks.Item2?.Invoke();
            }

            if (GameModeCallbacks.TryGetValue(gameMode, out var gameModeCallbacks))
            {
                gameModeCallbacks.Item1?.Invoke();
            }
            _prevGameMode = gameMode;
        }
    }

    internal static void ClearAllRegisteredFlowTransitions()
    {
        FlowTransitionCallback = null;
        GameModeCallbacks.Clear();
    }
}