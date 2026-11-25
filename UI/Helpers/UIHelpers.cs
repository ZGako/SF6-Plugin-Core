// Core usings

using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.Helpers;

/// <summary>
///     Provides lookup helpers for RE Engine GUI trees, <see cref="app.UIAgent" /> instances, and Training Pause
///     Menu custom function IDs.
/// </summary>
public static class UIHelpers
{
    /// <summary>
    ///     Searches the descendants of <paramref name="parent" /> for the first <see cref="via.gui.PlayObject" /> whose
    ///     <c>Name</c> matches <paramref name="childName" />.
    /// </summary>
    /// <param name="parent">The root GUI object whose children are queried via <c>getChildren(System.Type)</c>.</param>
    /// <param name="childName">The exact GUI node name (such as <c>e_text_0</c> or <c>p_InputGuide_2_s</c>).</param>
    /// <returns>The matching <see cref="ManagedObject" /> for the child <see cref="via.gui.PlayObject" />.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent" /> cannot be cast to <see cref="IObject" />.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no child with <paramref name="childName" /> exists under <paramref name="parent" />.</exception>
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

    /// <summary>
    ///     Returns all descendants of <paramref name="parent" /> matching <paramref name="childType" /> by invoking RE
    ///     Engine's <c>getChildren(System.Type)</c>.
    /// </summary>
    /// <param name="parent">The root GUI object to query.</param>
    /// <param name="childType">The TDB <see cref="TypeDefinition" /> passed to <c>getChildren</c>.</param>
    /// <returns>An engine <see cref="_System.Array" /> containing matching child GUI objects.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent" /> cannot be cast to <see cref="IObject" />.</exception>
    /// <exception cref="InvalidCastException">Thrown when the engine call does not return a <see cref="_System.Array" />.</exception>
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
    ///     Performs an O(n) linear search across <see cref="GameSingletonManager.UIAgentManager" /> entries to find the
    ///     <see cref="app.UIAgent" /> attached to the specified scene GameObject.
    /// </summary>
    /// <remarks>
    ///     Prefer caching the returned <see cref="app.UIAgent" /> rather than calling this method every frame.
    /// </remarks>
    /// <param name="gameObjectName">The exact <c>GameObject.Name</c> associated with the target UI agent.</param>
    /// <returns>The matching <see cref="app.UIAgent" /> instance.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <see cref="GameSingletonManager.UIAgentManager" /> is unavailable or no entry matches
    ///     <paramref name="gameObjectName" />.
    /// </exception>
    public static app.UIAgent GetUIAgent(string gameObjectName)
    {
        var uiAgents = GameSingletonManager.UIAgentManager?._Entries
                       ?? throw new InvalidOperationException(
                           "Call this method only if you can guarantee the UIAgentManger exists");

        // cannot use foreach it seems
        for (var i = 0; i < uiAgents.Count; i++)
        {
            var agent = uiAgents[i].Agent;
            if (agent.GameObject.Name == gameObjectName)
                return agent;
        }

        throw new InvalidOperationException($"{gameObjectName} not found in UI Agent Manager");
    }

    /// <summary>
    ///     Resolves the stable mod-defined or deterministic <see cref="Guid" /> mapped to an integer
    ///     <see cref="app.training.TrainingFuncType" /> ID for the current Training Pause Menu rebuild.
    /// </summary>
    /// <param name="funcType">The runtime integer value of <c>TrainingMenuData.FuncType</c>.</param>
    /// <param name="id">When this method returns <see langword="true" />, receives the mapped <see cref="Guid" />.</param>
    /// <returns><see langword="true" /> if <paramref name="funcType" /> is currently registered; otherwise <see langword="false" />.</returns>
    public static bool TryGetFunctionId(int funcType, out Guid id)
    {
        return FunctionTypeRegistry.TryGetFunctionId(funcType, out id);
    }

    /// <summary>
    ///     Resolves the runtime integer <see cref="app.training.TrainingFuncType" /> ID assigned to a custom element's
    ///     <see cref="Guid" /> during the current Training Pause Menu rebuild.
    /// </summary>
    /// <remarks>
    ///     Custom integer IDs are cleared and reallocated on every rebuild, so callers should query this mapping
    ///     dynamically rather than caching integer IDs across menu changes.
    /// </remarks>
    /// <param name="id">The custom function <see cref="Guid" /> passed to the node or request constructor.</param>
    /// <param name="funcType">When this method returns <see langword="true" />, receives the assigned integer function type.</param>
    /// <returns><see langword="true" /> if <paramref name="id" /> has been allocated in the current build; otherwise <see langword="false" />.</returns>
    public static bool TryGetFunctionType(Guid id, out int funcType)
    {
        return FunctionTypeRegistry.TryGetFunctionType(id, out funcType);
    }
}