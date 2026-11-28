namespace SF6_Plugin_Core.UI.ReplayPauseMenu.Core;

/// <summary>
///     Registry mapping mod-defined <see cref="Guid" /> identifiers and vanilla
///     <see cref="app.esports.UIReplayPauseMenu.EReplayFuncType" /> values to runtime integer IDs.
/// </summary>
internal static class ReplayFuncTypeRegistry
{
    private static readonly ConcurrentDictionary<Guid, int> ForwardLookup = [];
    private static readonly ConcurrentDictionary<int, Guid> ReverseLookup = [];
    private static int _customFunctionCount;

    public static readonly int ReplayFuncTypeMax = ComputeReplayFuncTypeMax();

    private static int ComputeReplayFuncTypeMax()
    {
        var maxVal = (int)app.esports.UIReplayPauseMenu.EReplayFuncType.Again;

        var typeDef = API.GetTDB().FindType("app.esports.UIReplayPauseMenu.EReplayFuncType");
        if (typeDef == null) return maxVal;

        foreach (var field in typeDef.GetFields())
        {
            if (!field.IsStatic() || !field.IsLiteral()) continue;

            var rawPtr = field.GetDataRaw(0, false);
            if (rawPtr == 0) continue;

            var val = Marshal.ReadInt32((nint)rawPtr);
            if (val > maxVal) maxVal = val;
        }

        return maxVal;
    }

    internal static int RegisterNewFunctionType(Guid id)
    {
        if (ForwardLookup.TryGetValue(id, out var existing))
            return existing;

        var funcType = ReplayFuncTypeMax + Interlocked.Increment(ref _customFunctionCount);
        ForwardLookup[id] = funcType;
        ReverseLookup[funcType] = id;
        return funcType;
    }

    internal static Guid RegisterGameFunctionType(app.esports.UIReplayPauseMenu.EReplayFuncType funcType)
    {
        var funcTypeInt = (int)funcType;
        if (ReverseLookup.TryGetValue(funcTypeInt, out var existingId)) return existingId;

        Guid id = new(funcTypeInt, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3);
        ForwardLookup[id] = funcTypeInt;
        ReverseLookup[funcTypeInt] = id;
        return id;
    }

    public static bool TryGetFunctionType(Guid id, out int funcType)
    {
        return ForwardLookup.TryGetValue(id, out funcType);
    }

    public static bool TryGetFunctionId(int funcType, out Guid id)
    {
        return ReverseLookup.TryGetValue(funcType, out id);
    }

    public static void Clear()
    {
        ForwardLookup.Clear();
        ReverseLookup.Clear();
        Interlocked.Exchange(ref _customFunctionCount, 0);
    }
}

