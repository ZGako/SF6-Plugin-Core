
namespace SF6_Plugin_Core;

public static class FlowTransitionDispatcher
{
    public readonly struct FlowTransitionStates
    {
        // information about a singular "state" that gets tracked
        public app.AppDefine.GameMode GameMode { get; init; }
        public app.constant.scn.Index SceneIndex { get; init; }
        public app.constant.FlowMap.eID FlowMapID { get; init; }
    }

    public delegate void FlowTransitionDelegate(FlowTransitionStates state);

    public class FlowTransitionHook(List<FlowTransitionStates> targetStates, FlowTransitionDelegate onEnterState, FlowTransitionDelegate onExitState)
    {
        private List<FlowTransitionStates> TargetStates { get; init; } = targetStates;

        public FlowTransitionDelegate OnEnterState { get; init; } = onEnterState;
        public FlowTransitionDelegate OnExitState { get; init; } = onExitState;

        public System.Collections.Generic.IReadOnlyList<FlowTransitionStates> GetTargetStates() => TargetStates.AsReadOnly();
    }

    // dictionary mapping our hooks to their current state (true if the we're in the target state, false if not)
    private static readonly Dictionary<FlowTransitionHook, bool> RegisteredFlowTransitions = [];

    public static void RegisterFlowTransition(FlowTransitionHook hook)
    {
        RegisteredFlowTransitions.Add(hook, false);
    }

    public static void UnregisterFlowTransition(FlowTransitionHook hook)
    {
        RegisteredFlowTransitions.Remove(hook);
    }

    // need to add the methodhook to the transition function and actually have the logic
    // to check the state against the registered hooks and call the appropriate delegate(s)

    // [MethodHook(typeof(app.bFlowManager), "transition(System.Collections.Generic.List`1<app.bFlowBase>, app.FlowMapBase, app.constant.scn.Index, app.bFlowBase)", MethodHookType.Post)]
    [MethodHook(typeof(app.bFlowManager.FlowWork), nameof(app.bFlowManager.FlowWork.transition), MethodHookType.Post)]
    private static void OnFlowTransition(ref ulong retval)
    {
        // early exit if there are no registered flow transitions
        if (RegisteredFlowTransitions.Count == 0) return;

        var flowManager = GameSingletonRegistry.BFlowManager!;

        // get the current values we check against the registered hooks
        var currentGameMode = flowManager.m_flow_work.GameMode;
        var currentSceneId = flowManager.m_flow_work.SceneId;
        var currentFlowMapID = flowManager.m_flow_work.FlowMap != null ? flowManager.m_flow_work.FlowMap.ID : app.constant.FlowMap.eID.UNKNOWN;

        foreach (var kvp in RegisteredFlowTransitions)
        {
            var hook = kvp.Key;
            var isInTargetState = kvp.Value;


            // check if the current state matches any of the target states for this hook
            bool matchesTargetState = hook.GetTargetStates().Any(targetState =>
                targetState.GameMode == currentGameMode &&
                targetState.SceneIndex == currentSceneId &&
                targetState.FlowMapID == currentFlowMapID
            );

            if (matchesTargetState && !isInTargetState)
            {
                // we have entered a target state
                hook.OnEnterState.Invoke(new FlowTransitionStates { GameMode = currentGameMode, SceneIndex = currentSceneId, FlowMapID = currentFlowMapID });
                RegisteredFlowTransitions[hook] = true;
            }
            else if (!matchesTargetState && isInTargetState)
            {
                // we have exited a target state
                hook.OnExitState.Invoke(new FlowTransitionStates { GameMode = currentGameMode, SceneIndex = currentSceneId, FlowMapID = currentFlowMapID });
                RegisteredFlowTransitions[hook] = false;
            }
        }
    }
}