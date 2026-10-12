global using System;
global using System.Collections.Generic;
global using System.Runtime.InteropServices;
global using System.Linq;
global using System.Collections.Concurrent;

global using REFrameworkNET;
global using REFrameworkNET.Attributes;
global using REFrameworkNET.Collections;
global using REFrameworkNET.Callbacks;

using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI;

namespace SF6_Plugin_Core;

//FIXME: refactor
public static class CoreLifecycle
{
    public static event Action? OnCoreUnloading;

    [PluginExitPoint]
    public static void PluginExitPoint()
    {
        OnCoreUnloading?.Invoke();

        OnCoreUnloading = null;

        GameSingletonRegistry.Clear();
        FlowTransitionDispatcher.ClearAllRegisteredFlowTransitions();
        BattleHudDispatcher.ClearAllRegisteredBattleHudCallbacks();
        RequestQueuePoller.Clear();
    }
}