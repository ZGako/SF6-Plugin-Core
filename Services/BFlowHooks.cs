namespace SF6_Plugin_Core.Services;

/// <summary>
///     Dispatches SF6 game flow state transitions (<see cref="app.bFlowManager.FlowWork.transition" />) to registered
///     listeners.
/// </summary>
public static class BFlowHooks
{
    /// <summary>
    ///     Callback invoked after <see cref="app.bFlowManager.FlowWork.transition" /> executes, as well as immediately
    ///     upon registration if <see cref="GameSingletonManager.BFlowManager" /> is already initialized.
    /// </summary>
    /// <param name="flowWork">The active <see cref="app.bFlowManager.FlowWork" /> representing the current game flow.</param>
    public delegate void FlowTransitionDelegate(app.bFlowManager.FlowWork flowWork);

    private static event FlowTransitionDelegate? FlowTransitionCallback;

    /// <summary>
    ///     Subscribes a handler to every <see cref="app.bFlowManager.FlowWork.transition" /> call, and invokes it
    ///     immediately with the active flow work if <see cref="GameSingletonManager.BFlowManager" /> is already ready.
    /// </summary>
    /// <param name="flowTransitionDelegate">
    ///     The callback to invoke on flow changes. Because it fires on every transition while registered, handlers must
    ///     check the received <c>flowWork</c> themselves to distinguish entering vs. leaving a target flow.
    /// </param>
    public static void RegisterFlowTransitionCallback(FlowTransitionDelegate flowTransitionDelegate)
    {
        FlowTransitionCallback += flowTransitionDelegate;

        if (GameSingletonManager.BFlowManager == null) return; // BFlowManager is not ready yet, exit early
        // invoke to make sure the callback is aware of the current flow work
        flowTransitionDelegate?.Invoke(GameSingletonManager.BFlowManager.m_flow_work);
    }

    /// <summary>
    ///     Unsubscribes a handler previously registered with <see cref="RegisterFlowTransitionCallback" />.
    /// </summary>
    /// <param name="flowTransitionDelegate">The delegate instance to remove.</param>
    public static void UnregisterFlowTransitionCallback(FlowTransitionDelegate flowTransitionDelegate)
    {
        FlowTransitionCallback -= flowTransitionDelegate;
    }

    /// <summary>
    ///     REFramework post-hook on <see cref="app.bFlowManager.FlowWork.transition" /> (not intended for direct calls
    ///     by mods). Forwards the active <c>m_flow_work</c> to all subscribed callbacks.
    /// </summary>
    /// <param name="retval">Raw return value from the hooked <c>transition</c> call (unused).</param>
    [MethodHook(typeof(app.bFlowManager.FlowWork), nameof(app.bFlowManager.FlowWork.transition), MethodHookType.Post)]
    public static void OnFlowTransition(ref ulong retval)
    {
        var threadSafeCopy = FlowTransitionCallback; // Make a thread-safe copy of the delegate
        if (threadSafeCopy == null) return; // No callbacks registered, exit early

        try
        {
            var flowWork = GameSingletonManager.BFlowManager?.m_flow_work;
            if (flowWork == null) return;

            threadSafeCopy.Invoke(flowWork);
        }
        catch (Exception ex)
        {
            API.LogError($"[BFlowHooks] Exception in OnFlowTransition: {ex}");
        }
    }

    internal static void ClearAllRegisteredFlowTransitions()
    {
        FlowTransitionCallback = null;
    }
}