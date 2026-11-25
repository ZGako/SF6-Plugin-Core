namespace SF6_Plugin_Core.Memory;

/// <summary>
///     Thread-safe cache for globalized RE Engine <see cref="SystemString" /> instances, eliminating repeated
///     unmanaged string allocations across frame updates.
/// </summary>
/// <remarks>
///     Allocating strings on the RE Engine managed heap via <c>VM.CreateString</c> is relatively costly and
///     leaves objects eligible for engine GC unless globalized. Keep usage scoped to a bounded set of recurring UI
///     labels (such as formatted spinner values or custom input guide markup), because pooled strings remain pinned
///     via <c>Globalize()</c> until <see cref="Clear" /> or <see cref="Dispose" /> runs.
/// </remarks>
public sealed class ManagedStringPool : IDisposable
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, SystemString> _pool = [];

    /// <summary>
    ///     Releases all globalized RE Engine strings held by this pool and clears the cache.
    /// </summary>
    public void Dispose()
    {
        Clear();
    }

    /// <summary>
    ///     Returns a cached globalized <see cref="SystemString" /> for <paramref name="text" />, or allocates,
    ///     globalizes, and caches a new instance on first lookup.
    /// </summary>
    /// <param name="text">The managed C# string whose RE Engine representation should be retrieved or allocated.</param>
    /// <returns>
    ///     A globalized <see cref="SystemString" /> safe to pass into RE Engine calls across frames until the pool is
    ///     cleared.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <c>VM.CreateString</c> fails to allocate the string in the RE Engine VM.
    /// </exception>
    public SystemString GetOrCreate(string text)
    {
        lock (_lock)
        {
            if (!_pool.TryGetValue(text, out var engineStr))
            {
                engineStr = VM.CreateString(text)
                            ?? throw new InvalidOperationException("Failed to create engine string.");
                engineStr.Globalize();
                _pool[text] = engineStr;
            }

            return engineStr;
        }
    }

    /// <summary>
    ///     Releases all globalized RE Engine strings retained by this pool and clears the internal cache.
    /// </summary>
    /// <remarks>
    ///     Invokes <c>ReleaseIfGlobalized()</c> on each pooled string so that the RE Engine runtime can reclaim
    ///     the underlying unmanaged memory.
    /// </remarks>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var str in _pool.Values) str.ReleaseIfGlobalized();
            _pool.Clear();
        }
    }
}