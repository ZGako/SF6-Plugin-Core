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
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.OptionsSettings.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core;

public static class CoreLifecycle
{
    public static event Action? OnCoreUnloading;

    [PluginExitPoint]
    public static void PluginExitPoint()
    {
        OnCoreUnloading?.Invoke();

        OnCoreUnloading = null;

        // clear services
        GameSingletonManager.Clear();
        BFlowHooks.ClearAllRegisteredFlowTransitions();
        FrameCallbackPoller.Clear();

        // clear battle hud hooks
        BattleHudHooks.ClearAllRegisteredBattleHudCallbacks();

        // clear localization
        MessageRegistry.Clear();

        // clear training pause menu
        PauseMenuManager.ClearTrainingPauseMenuHooks();

        // clear options settings hook
        OptionsSettingsManager.ClearOptionsSettingsHooks();
    }
}