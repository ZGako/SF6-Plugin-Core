using SF6_Plugin_Core.Services;

namespace SF6_Plugin_Core.UI;

/// <summary>
/// Provides factory helpers for creating and materializing UI prefabs from a registered resource path.
/// </summary>
public static class UIPrefabFactory
{
    /// <summary>
    /// Creates a live prefab instance from the specified resource path and returns the instantiated game object.
    /// </summary>
    /// <param name="prefabPath">
    /// The prefab resource path to load. This value must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The created and globalized <see cref="ManagedObject"/> instance for the prefab.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the resource manager is unavailable, the prefab cannot be created, or the prefab instance cannot be instantiated.
    /// </exception>
    public static ManagedObject CreateUIPrefab(string prefabPath)
    {
        if (GameSingletonRegistry.ResourceManager == null)
        {
            throw new InvalidOperationException("UIPrefabManager is not available. Ensure it is initialized before creating UI prefabs.");
        }

        var prefabRes = GameSingletonRegistry.ResourceManager.CreateResource("via.PrefabResource", prefabPath);
        if (prefabRes == null)
        {
            throw new InvalidOperationException($"Failed to create prefab resource from path: {prefabPath}");
        }

        try
        {
            var prefabHolder = prefabRes.CreateHolder("via.PrefabResourceHolder")?.As<via.PrefabResourceHolder>();
            if (prefabHolder == null)
            {
                throw new InvalidOperationException($"Failed to create prefab holder for prefab: {prefabPath}");
            }

            var prefabMo = via.Prefab.REFType.CreateInstance(0);

            var prefab = prefabMo.As<via.Prefab>();
            if (prefab == null)
            {
                throw new InvalidOperationException($"Failed to create prefab instance for prefab: {prefabPath}");
            }
            prefab.Path = prefabHolder.ResourcePath;

            // 0,0,0 position required for parameter
            var pos = via.vec3.REFType.CreateValueType().As<via.vec3>();

            var gameObjectMo = (prefab as IObject)?.Call("instantiate(via.vec3)", pos) as ManagedObject;
            if (gameObjectMo == null)
            {
                throw new InvalidOperationException($"Failed to instantiate prefab: {prefabPath}");
            }
            gameObjectMo.Globalize();

            return gameObjectMo;
        }
        finally
        {
            prefabRes.Release();
        }
    }
}