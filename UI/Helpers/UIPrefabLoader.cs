using SF6_Plugin_Core.Services;

namespace SF6_Plugin_Core.UI.Helpers;

/// <summary>
///     Provides factory helpers for creating and materializing UI prefabs from a registered resource path.
/// </summary>
public static class UIPrefabLoader
{
    /// <summary>
    ///     Creates a live prefab instance from the specified resource path and returns the instantiated game object.
    /// </summary>
    /// <param name="prefabPath">
    ///     The prefab resource path to load. This value must not be <see langword="null" />.
    /// </param>
    /// <returns>
    ///     The created and globalized <see cref="ManagedObject" /> instance for the prefab.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the resource manager is unavailable, the prefab cannot be created, or the prefab instance cannot be
    ///     instantiated.
    /// </exception>
    public static void CreateUIPrefab(string prefabPath, Action<ManagedObject> onPrefabCreated)
    {
        if (GameSingletonManager.ResourceManager == null)
            throw new InvalidOperationException(
                "UIPrefabManager is not available. Ensure it is initialized before creating UI prefabs.");

        var prefabRes = GameSingletonManager.ResourceManager.CreateResource("via.PrefabResource", prefabPath);
        if (prefabRes == null)
            throw new InvalidOperationException($"Failed to create prefab resource from path: {prefabPath}");

        var prefabHolder = prefabRes.CreateHolder("via.PrefabResourceHolder")?.As<via.PrefabResourceHolder>();
        if (prefabHolder == null)
        {
            prefabRes.Release();
            throw new InvalidOperationException($"Failed to create prefab holder for prefab: {prefabPath}");
        }

        var prefabMo = via.Prefab.REFType.CreateInstance(0);
        prefabMo.Globalize();

        var prefab = prefabMo.As<via.Prefab>();
        if (prefab == null)
        {
            prefabMo.ReleaseIfGlobalized();
            prefabRes.Release();
            throw new InvalidOperationException($"Failed to create prefab instance for prefab: {prefabPath}");
        }

        prefab.Path = prefabHolder.ResourcePath;

        FrameCallbackPoller.WaitUntil(
            () => prefab.Ready,
            () =>
            {
                try
                {
                    var pos = via.vec3.REFType.CreateValueType().As<via.vec3>();
                    var gameObjectMo = (prefab as IObject)?.Call("instantiate(via.vec3)", pos) as ManagedObject
                                       ?? throw new InvalidOperationException(
                                           $"Failed to instantiate prefab: {prefabPath}");

                    gameObjectMo.Globalize();
                    onPrefabCreated(gameObjectMo);
                }
                finally
                {
                    Cleanup();
                }
            },
            () => { Cleanup(); },
            60
        );

        void Cleanup()
        {
            prefabMo.ReleaseIfGlobalized();
            prefabRes.Release();
        }
    }
}