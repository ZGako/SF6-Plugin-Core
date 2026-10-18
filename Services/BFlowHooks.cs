namespace SF6_Plugin_Core.Services;

/// <summary>
///     Dispatches flow transition notifications to registered callbacks and game-mode handlers.
/// </summary>
public static class BFlowHooks
{
    /// <summary>
    ///     Represents a callback invoked when a flow transition occurs.
    /// </summary>
    /// <param name="flowWork">The flow work being transitioned.</param>
    public delegate void FlowTransitionDelegate(app.bFlowManager.FlowWork flowWork);

    private static event FlowTransitionDelegate? FlowTransitionCallback;

    /// <summary>
    ///     Registers a callback to run for every flow transition. It also invokes the callback immediately with the current
    ///     flow work if available.
    /// </summary>
    /// <param name="flowTransitionDelegate">The callback to invoke with the current flow work.</param>
    /// <remarks>
    ///     The callback will be invoked every time, make sure it handles entering and exiting of the bFlow properly on
    ///     its own.
    /// </remarks>
    public static void RegisterFlowTransitionCallback(FlowTransitionDelegate flowTransitionDelegate)
    {
        FlowTransitionCallback += flowTransitionDelegate;

        if (GameSingletonManager.BFlowManager == null) return; // BFlowManager is not ready yet, exit early
        // invoke to make sure the callback is aware of the current flow work
        flowTransitionDelegate?.Invoke(GameSingletonManager.BFlowManager.m_flow_work);
    }

    /// <summary>
    ///     Removes a callback previously registered through
    ///     <see cref="RegisterFlowTransitionCallback(FlowTransitionDelegate)" />.
    /// </summary>
    /// <param name="flowTransitionDelegate">The callback to unregister.</param>
    public static void UnregisterFlowTransitionCallback(FlowTransitionDelegate flowTransitionDelegate)
    {
        FlowTransitionCallback -= flowTransitionDelegate;
    }

    [MethodHook(typeof(app.bFlowManager.FlowWork), nameof(app.bFlowManager.FlowWork.transition), MethodHookType.Post)]
    public static void OnFlowTransition(ref ulong retval)
    {
        if (FlowTransitionCallback == null) return; // No callbacks registered, exit early

        var flowWork = GameSingletonManager.BFlowManager!.m_flow_work;

        var threadSafeCopy = FlowTransitionCallback; // Make a thread-safe copy of the delegate
        threadSafeCopy?.Invoke(flowWork);
    }

    internal static void ClearAllRegisteredFlowTransitions()
    {
        FlowTransitionCallback = null;
    }
}