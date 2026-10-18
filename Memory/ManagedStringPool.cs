namespace SF6_Plugin_Core.Memory;

/// <summary>
///     Caches and reuses REFramework RE Engine strings (<see cref="SystemString" />) to avoid repeated unmanaged
///     allocations.
/// </summary>
/// <remarks>
///     Allocating RE Engine strings in unmanaged memory via <see cref="VM.CreateString" /> is expensive.
///     Pooling these instances mitigates allocation overhead and reduces engine garbage collector churn.
///     <para>
///         This pool is best suited for a bounded, recurring set of strings (e.g., UI labels, state identifiers,
///         or lookup keys) that are frequently passed into engine calls. It should not be used for arbitrarily generated
///         or unbounded strings, as pooled instances remain globalized until explicitly cleared or disposed.
///     </para>
/// </remarks>
public sealed class ManagedStringPool : IDisposable
{
    private readonly Dictionary<string, SystemString> _pool = [];

    /// <summary>
    ///     Disposes the pool and frees all pooled RE Engine strings by invoking <see cref="Clear" />.
    /// </summary>
    public void Dispose()
    {
        Clear();
    }

    /// <summary>
    ///     Retrieves a cached <see cref="SystemString" /> for the specified text, or allocates, globalizes, and pools a new RE
    ///     Engine string.
    /// </summary>
    /// <remarks>
    ///     Newly allocated strings are globalized to prevent the RE Engine garbage collector from prematurely
    ///     reclaiming them across frames while they are retained by the pool.
    /// </remarks>
    /// <param name="text">The string text to retrieve from cache or allocate in engine memory.</param>
    /// <returns>A persistent, globalized <see cref="SystemString" /> instance suitable for passing to the RE Engine.</returns>
    /// <exception cref="InvalidOperationException">Thrown when REFramework fails to allocate the string in the RE Engine.</exception>
    public SystemString GetOrCreate(string text)
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

    /// <summary>
    ///     Releases all globalized RE Engine strings retained by this pool and clears the internal cache.
    /// </summary>
    /// <remarks>
    ///     Invokes <c>ReleaseIfGlobalized()</c> on each pooled string so that the RE Engine runtime can reclaim
    ///     the underlying unmanaged memory.
    /// </remarks>
    public void Clear()
    {
        foreach (var str in _pool.Values) str.ReleaseIfGlobalized();
        _pool.Clear();
    }
}