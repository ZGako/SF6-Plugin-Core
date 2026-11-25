using SF6_Plugin_Core.Services;

namespace SF6_Plugin_Core.UI.Helpers;

/// <summary>
///     Asynchronously loads <c>via.PrefabResource</c> assets via <see cref="GameSingletonManager.ResourceManager" />
///     and instantiates their scene <see cref="via.GameObject" /> once ready.
/// </summary>
public static class UIPrefabLoader
{
    /// <summary>
    ///     Requests a <c>via.PrefabResource</c> at <paramref name="prefabPath" />, polls for up to 60 frames on the
    ///     main thread until the prefab finishes loading, instantiates it at the origin, and transfers the globalized
    ///     <see cref="via.GameObject" /> <see cref="ManagedObject" /> to <paramref name="onPrefabCreated" />.
    /// </summary>
    /// <remarks>
    ///     Ownership of the instantiated <see cref="ManagedObject" /> transfers to <paramref name="onPrefabCreated" />,
    ///     which is responsible for destroying and releasing it when no longer needed. If <paramref name="onPrefabCreated" />
    ///     throws, this loader destroys the <see cref="via.GameObject" /> and releases its handle automatically. If the
    ///     prefab fails to become ready within 60 frames, intermediate resource and prefab handles are released silently
    ///     without invoking <paramref name="onPrefabCreated" />.
    /// </remarks>
    /// <param name="prefabPath">Engine resource path of the UI prefab (for example, a <c>.pfb</c> asset path).</param>
    /// <param name="onPrefabCreated">Callback invoked on the main thread with the globalized instantiated <see cref="via.GameObject" />.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown synchronously if <see cref="GameSingletonManager.ResourceManager" /> is unavailable or if RE Engine
    ///     refuses to allocate the prefab resource, holder, or <see cref="via.Prefab" /> instance.
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
                ManagedObject? gameObjectMo = null;
                try
                {
                    var pos = via.vec3.REFType.CreateValueType().As<via.vec3>();
                    gameObjectMo = (prefab as IObject)?.Call("instantiate(via.vec3)", pos) as ManagedObject
                                   ?? throw new InvalidOperationException(
                                       $"Failed to instantiate prefab: {prefabPath}");

                    gameObjectMo.Globalize();
                    onPrefabCreated(gameObjectMo);
                    gameObjectMo = null; // ownership transferred to callback
                }
                catch (Exception ex)
                {
                    API.LogError($"[UIPrefabLoader] Error creating UI prefab '{prefabPath}': {ex}");
                    if (gameObjectMo != null)
                    {
                        try
                        {
                            via.GameObject.destroy(gameObjectMo.As<via.GameObject>());
                        }
                        catch
                        {
                            // Best-effort engine destroy on callback failure
                        }

                        gameObjectMo.ReleaseIfGlobalized();
                    }
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