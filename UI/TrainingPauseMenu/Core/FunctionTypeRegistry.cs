namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

/// <summary>
///     Registry for mapping custom and game-specific training function types and names.
/// </summary>
internal static class FunctionTypeRegistry
{
    // Store all new allocated functionTypes here
    private static readonly ConcurrentDictionary<Guid, int> ForwardLookup = [];
    private static readonly ConcurrentDictionary<int, Guid> ReverseLookup = [];
    private static int _customFunctionCount;

    public static readonly int TrainingFuncTypeMax =
        Convert.ToInt32(API.GetTDB().FindType("app.training.TrainingFuncType").GetField("MAX").GetDataBoxed(0, false));

    /// <summary>
    ///     Registers a new custom training function type with a unique ID and returns it.
    /// </summary>
    /// <param name="id">The id of the custom function type.</param>
    /// <returns>The assigned unique integer function type ID.</returns>
    internal static int RegisterNewFunctionType(Guid id)
    {
        // since the mappings have to be unique, check if the name is already registered
        if (ForwardLookup.TryGetValue(id, out var value))
        {
            API.LogWarning($"Custom Function type '{id}' is already registered.");
            return value;
        }

        var funcType = TrainingFuncTypeMax + Interlocked.Increment(ref _customFunctionCount);

        ForwardLookup[id] = funcType;
        ReverseLookup[funcType] = id;

        return ForwardLookup[id];
    }

    /// <summary>
    ///     Registers an existing game training function type and maps its string representation to its integer value.
    /// </summary>
    /// <param name="funcType">The game training function type to register.</param>
    /// <returns>The unique ID of the function type.</returns>
    internal static Guid RegisterGameFunctionType(app.training.TrainingFuncType funcType)
    {
        var funcTypeInt = (int)funcType;

        // since the mappings have to be unique, check if the name is already registered
        if (ReverseLookup.TryGetValue(funcTypeInt, out var existingId)) return existingId;

        // Deterministic Guid for vanilla TrainingFuncType so it stays identical across rebuilds
        Guid id = new(funcTypeInt, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);

        ForwardLookup[id] = funcTypeInt;
        ReverseLookup[funcTypeInt] = id;

        return id;
    }

    /// <summary>
    ///     Attempts to retrieve the function type ID associated with the specified name.
    /// </summary>
    /// <param name="id">The ID of the function type.</param>
    /// <param name="funcType">When this method returns, contains the function type ID if found.</param>
    /// <returns><c>true</c> if the function type was found; otherwise, <c>false</c>.</returns>
    public static bool TryGetFunctionType(Guid id, out int funcType)
    {
        return ForwardLookup.TryGetValue(id, out funcType);
    }

    /// <summary>
    ///     Attempts to retrieve the function name associated with the specified function type ID.
    /// </summary>
    /// <param name="funcType">The function type ID.</param>
    /// <param name="id">When this method returns, contains the function ID if found.</param>
    /// <returns><c>true</c> if the function ID was found; otherwise, <c>false</c>.</returns>
    public static bool TryGetFunctionId(int funcType, out Guid id)
    {
        return ReverseLookup.TryGetValue(funcType, out id);
    }

    /// <summary>
    ///     Clears all registered function type lookups.
    /// </summary>
    public static void Clear()
    {
        ForwardLookup.Clear();
        ReverseLookup.Clear();
        Interlocked.Exchange(ref _customFunctionCount, 0);
    }
}