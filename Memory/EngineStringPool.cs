

namespace SF6_Plugin_Core.Memory;


public sealed class EngineStringPool : IDisposable
{
    private readonly Dictionary<string, SystemString> _pool = [];

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

    public void Clear()
    {
        foreach (var str in _pool.Values)
        {
            str.ReleaseIfGlobalized();
        }
        _pool.Clear();
    }

    public void Dispose() => Clear();
}