// Core usings
using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.Localization;
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
        if ((parent as IObject) == null)
        {
            throw new ArgumentNullException(nameof(parent), "Parent PlayObject cannot be null.");
        }

        var guiChildrenArray = (parent as IObject)!.Call("getChildren(System.Type)", PlayObjectType).GetObjectAs<_System.Array>($"Failed to retrieve children for PlayObject '{parent}'.");
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

        if ((parent as IObject) == null)
        {
            throw new ArgumentNullException(nameof(parent), "Parent PlayObject cannot be null.");
        }

        var guiChildrenArray = (parent as IObject)!.Call("getChildren(System.Type)", SearchType).GetObjectAs<_System.Array>("Failed to retrieve children as _System.Array.");

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