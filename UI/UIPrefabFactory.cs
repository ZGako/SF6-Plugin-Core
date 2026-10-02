

using SF6_Plugin_Core;

namespace SF6_Plugin_Core.UI;

public static class UIPrefabFactory
{
    public static ManagedObject CreateUIPrefab(string prefabPath)
    {
        if (GameSingletonRegistry.ResourceManager == null)
        {
            throw new InvalidOperationException("UIPrefabManager is not available. Ensure it is initialized before creating UI prefabs.");
        }

        // using means the IDisposable will call Dispose() automatically at the end of the using block, which is important for resource management
        using var prefabRes = GameSingletonRegistry.ResourceManager.CreateResource("via.PrefabResource", prefabPath);
        if (prefabRes == null)
        {
            throw new InvalidOperationException($"Failed to create prefab resource from path: {prefabPath}");
        }

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

}