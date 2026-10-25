namespace SF6_Plugin_Core.UI.OptionsSettings.Core;

internal static class OptionTypeRegistry
{
    private static readonly ConcurrentDictionary<Guid, int> ForwardLookup = [];
    private static readonly ConcurrentDictionary<int, Guid> ReverseLookup = [];
    private static int _customOptionCount;

    private static readonly int OptionTypeMax;

    static OptionTypeRegistry()
    {
        var maxVal = 0;

        var typeDefValue = API.GetTDB().FindType("app.Option.ValueType");
        if (typeDefValue == null) return;
        var fields = typeDefValue.GetFields();

        foreach (var field in fields)
            if (field.IsStatic() && field.IsLiteral())
            {
                // Enum fields are static, so object address 0 is passed. 
                // isValueType is false because we are not targeting a struct instance.
                var val = Convert.ToInt32(field.GetDataBoxed(0, false));
                if (val > maxVal) maxVal = val;
            }

        OptionTypeMax = maxVal;

        var typeDefGroup = API.GetTDB().FindType("app.Option.GroupType");
        if (typeDefGroup == null) return;
        fields = typeDefGroup.GetFields();

        foreach (var field in fields)
            if (field.IsStatic() && field.IsLiteral())
            {
                // Enum fields are static, so object address 0 is passed. 
                // isValueType is false because we are not targeting a struct instance.
                var val = Convert.ToInt32(field.GetDataBoxed(0, false));
                if (val > maxVal) maxVal = val;
            }

        OptionTypeMax = maxVal;
    }

    internal static int RegisterNewOptionType(Guid id)
    {
        // since the mappings have to be unique, check if the name is already registered
        if (ForwardLookup.TryGetValue(id, out var value))
        {
            API.LogWarning($"Custom Function type '{id}' is already registered.");
            return value;
        }

        var funcType = OptionTypeMax + Interlocked.Increment(ref _customOptionCount);

        ForwardLookup[id] = funcType;
        ReverseLookup[funcType] = id;

        return ForwardLookup[id];
    }

    /// <summary>
    ///     Attempts to retrieve the option type ID associated with the specified name.
    /// </summary>
    /// <param name="id">The ID of the function type.</param>
    /// <param name="optionType">When this method returns, contains the function type ID if found.</param>
    /// <returns><c>true</c> if the function type was found; otherwise, <c>false</c>.</returns>
    public static bool TryGetOptionType(Guid id, out int optionType)
    {
        return ForwardLookup.TryGetValue(id, out optionType);
    }

    /// <summary>
    ///     Attempts to retrieve the option id associated with the specified option type value.
    /// </summary>
    /// <param name="optionType">The function type ID.</param>
    /// <param name="id">When this method returns, contains the function ID if found.</param>
    /// <returns><c>true</c> if the optionType was found; otherwise, <c>false</c>.</returns>
    public static bool TryGetOptionId(int optionType, out Guid id)
    {
        return ReverseLookup.TryGetValue(optionType, out id);
    }

    /// <summary>
    ///     Clears all registered option type lookups.
    /// </summary>
    public static void Clear()
    {
        ForwardLookup.Clear();
        ReverseLookup.Clear();
        Interlocked.Exchange(ref _customOptionCount, 0);
    }
}