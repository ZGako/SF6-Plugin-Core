global using System;
global using System.Collections.Generic;
global using System.Runtime.InteropServices;
global using System.Linq;

global using REFrameworkNET;
global using REFrameworkNET.Attributes;
global using REFrameworkNET.Collections;
global using REFrameworkNET.Callbacks;

namespace SF6_Plugin_Core;

public class PluginCore
{

    [PluginEntryPoint]
    public static void CoreEntry()
    {
        API.LogInfo("SF6 Plugin Core loaded successfully.");
    }

    [PluginExitPoint]
    public static void CoreExit()
    {
        API.LogInfo("SF6 Plugin Core unloaded.");
    }
}
