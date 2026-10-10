// Core usings
using SF6_Plugin_Core.UI.TrainingPauseMenu.Dispatchers;

namespace SF6_Plugin_Core.UI;

//FIXME: refactor
/// <summary>
/// A static class containing helper methods for UI-related operations.
/// </summary>
public static class UIHelpers
{

    public static ManagedObject GetGuiChild(via.gui.PlayObject parent, string childName)
    {
        var PlayObjectType = via.gui.PlayObject.REFType.RuntimeType;

        var guiChildrenMo = (parent as IObject)?.Call("getChildren(System.Type)", PlayObjectType) as ManagedObject;

        if (guiChildrenMo == null)
        {
            throw new InvalidOperationException($"Failed to retrieve children for PlayObject '{parent}'.");
        }

        var guiChildrenArray = guiChildrenMo.As<_System.Array>();
        // don't bother checking for cast, as it should always work.

        foreach (var child in guiChildrenArray)
        {
            var childMo = child as ManagedObject;
            if (childMo == null) continue;

            var childPlayObject = childMo.As<via.gui.PlayObject>();
            if (childPlayObject == null) continue;

            if (childPlayObject.Name == childName)
            {
                return childMo;
            }
        }

        throw new InvalidOperationException($"Child with name '{childName}' not found in PlayObject '{parent}'.");
    }

    public static _System.Array GetGuiChildrenOfType(via.gui.PlayObject parent, TypeDefinition childType)
    {
        var SearchType = childType.RuntimeType;

        var guiChildrenMo = (parent as IObject)?.Call("getChildren(System.Type)", SearchType) as ManagedObject;

        if (guiChildrenMo == null)
        {
            throw new InvalidOperationException($"Failed to retrieve children for PlayObject '{parent}'.");
        }

        var guiChildrenArray = guiChildrenMo.As<_System.Array>();
        // don't bother checking for cast, as it should always work.

        return guiChildrenArray;
    }


    /// <summary>
    /// Clears all registered custom function types and their associated delegates in the Training Pause Menu system.
    /// </summary>
    public static void ClearTrainingPauseMenuDispatchers()
    {
        // Clear all registered custom function types and their associated delegates
        MessageManager.Clear();
        FunctionTypeRegistry.Clear();
        TrainingFunctionDispatcher.Clear();
        SpinBoxDispatcher.Clear();
        InputGuideDispatcher.Clear();
    }
}