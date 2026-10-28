// Core usings

using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.Services;

namespace SF6_Plugin_Core.UI.Helpers;

/// <summary>
///     A static class containing helper methods for UI-related operations.
/// </summary>
public static class UIHelpers
{
    public static ManagedObject GetGuiChild(this via.gui.PlayObject parent, string childName)
    {
        var PlayObjectType = via.gui.PlayObject.REFType.RuntimeType;
        if (parent as IObject == null)
            throw new ArgumentNullException(nameof(parent), "Parent PlayObject cannot be null.");

        var guiChildrenArray = (parent as IObject)!.Call("getChildren(System.Type)", PlayObjectType)
            .GetObjectAs<_System.Array>($"Failed to retrieve children for PlayObject '{parent}'.");
        // don't bother checking for cast, as it should always work.

        foreach (var child in guiChildrenArray)
        {
            var childMo = child as ManagedObject;
            if (childMo == null) continue;

            var childPlayObject = childMo.As<via.gui.PlayObject>();
            if (childPlayObject == null) continue;

            if (childPlayObject.Name == childName) return childMo;
        }

        throw new InvalidOperationException($"Child with name '{childName}' not found in PlayObject '{parent}'.");
    }

    public static _System.Array GetGuiChildrenOfType(this via.gui.PlayObject parent, TypeDefinition childType)
    {
        var SearchType = childType.RuntimeType;

        if (parent as IObject == null)
            throw new ArgumentNullException(nameof(parent), "Parent PlayObject cannot be null.");

        var guiChildrenArray = (parent as IObject)!.Call("getChildren(System.Type)", SearchType)
            .GetObjectAs<_System.Array>("Failed to retrieve children as _System.Array.");

        return guiChildrenArray;
    }

    /// <summary>
    /// O(n) lookup of the UIAgent associated with the GameObject whose name is the input parameter.
    /// </summary>
    /// <remarks>
    /// Make sure to store the UIAgent to avoid recalling this function.</remarks>
    /// <param name="gameObjectName"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static app.UIAgent GetUIAgent(string gameObjectName)
    {
        var uiAgents = GameSingletonManager.UIAgentManager?._Entries 
                          ?? throw new InvalidOperationException("Call this method only if you can guarantee the UIAgentManger exists");

        // cannot use foreach it seems
        for (var i = 0; i < uiAgents.Count; i++)
        {
            var agent = uiAgents[i].Agent;
            if (agent.GameObject.Name ==  gameObjectName)
                return agent;
        }
        throw new InvalidOperationException($"{gameObjectName} not found in UI Agent Manager");
    }
}