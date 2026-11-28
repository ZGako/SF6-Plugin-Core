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
using SF6_Plugin_Core.UI.ReplayPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core;

/// <summary>
///     Coordinates shutdown teardown across all <c>SF6_Plugin_Core</c> subsystems when REFramework unloads
///     the shared core plugin assembly.
/// </summary>
public static class CoreLifecycle
{
    /// <summary>
    ///     Raised at the start of core plugin teardown, before UI modifications, singletons, hooks, and localized
    ///     messages are cleared. Consumer plugins should subscribe to detach their callbacks and save state so no
    ///     references into consumer assemblies remain in <c>00_SF6PluginCore</c>.
    /// </summary>
    public static event Action? OnCoreUnloading;

    /// <summary>
    ///     REFramework plugin exit callback (not intended for direct calls by mods).
    ///     Notifies <see cref="OnCoreUnloading" /> subscribers, restores modified Training Pause Menu and Options
    ///     UI data if still live, and tears down shortcuts, singleton caches, flow/HUD hooks, and localization tables.
    /// </summary>
    [PluginExitPoint]
    public static void PluginExitPoint()
    {
        OnCoreUnloading?.Invoke();

        OnCoreUnloading = null;

        // clear training pause menu and options settings before clearing singletons/messages
        // so any remaining registered modifications can be cleanly restored first
        PauseMenuManager.ClearTrainingPauseMenuHooks();
        OptionsSettingsManager.ClearOptionsSettingsHooks();
        ReplayPauseMenuManager.ClearReplayPauseMenuHooks();
        ShortcutManager.ClearShortcuts();

        // clear services
        GameSingletonManager.Clear();
        BFlowHooks.ClearAllRegisteredFlowTransitions();
        FrameCallbackPoller.Clear();

        // clear battle hud hooks
        BattleHudHooks.ClearAllRegisteredBattleHudCallbacks();

        // clear localization
        MessageRegistry.Clear();
    }
}