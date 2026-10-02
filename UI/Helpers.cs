// Core usings
using SF6_Plugin_Core.UI.TrainingPauseMenu.Dispatchers;

namespace SF6_Plugin_Core.UI;

/// <summary>
/// A static class containing helper methods for UI-related operations.
/// </summary>
public static class UIHelpers
{

    /// <summary>
    /// Attempts to retrieve a managed object of type T from a given memory address.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="address"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidCastException"></exception>
    public static T GetAddressAs<T>(ulong address) where T : class
    {
        var managedObj = ManagedObject.ToManagedObject(address)
            ?? throw new ArgumentException($"Failed to convert pointer address {address} to a ManagedObject.");

        var actualType = managedObj.GetTypeDefinition();

        // FAST PATH: Reads the instantly accessible cached variable from our nested class
        var requestedType = TypeDefCache<T>.RequestedType;

        if (actualType != null && requestedType != null)
        {
            if (!actualType.IsDerivedFrom(requestedType))
            {
                throw new InvalidCastException(
                    $"Strict Cast Failed: Object at address {address} is of type '{actualType.FullName}', " +
                    $"which does not match or derive from the requested type '{requestedType.FullName}'.");
            }
        }
        else
        {
            // API.LogWarning($"Type definitions missing for strict validation on {address}. Proceeding with blind cast.");
        }

        return managedObj.As<T>() ?? throw new InvalidCastException($"Proxy creation failed for object at address {address}.");
    }

    /// <summary>
    /// A nested static class that caches the TypeDefinition of a given type T for fast access.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    private static class TypeDefCache<T> where T : class
    {
        public static readonly TypeDefinition? RequestedType;

        static TypeDefCache()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;

            // Your exact reflection logic, but it only ever runs once!
            RequestedType = (typeof(T).GetField("REFType", flags)?.GetValue(null)
                          ?? typeof(T).GetProperty("REFType", flags)?.GetValue(null)) as TypeDefinition;
        }
    }

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