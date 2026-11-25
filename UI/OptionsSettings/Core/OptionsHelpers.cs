namespace SF6_Plugin_Core.UI.OptionsSettings.Core;

/// <summary>
///     Lookup utilities bridging RE Engine Options UI visual controls (<see cref="app.UIPartsGroup" />) and custom
///     option <see cref="Guid" /> identifiers allocated by <see cref="OptionsSettingsManager" />.
/// </summary>
public static class OptionsHelpers
{
    /// <summary>
    ///     Searches the visual children of <paramref name="partsGroup" /> for the <see cref="app.UIPartsOptionUnit" />
    ///     bound to <paramref name="customOptionId" />.
    /// </summary>
    /// <param name="partsGroup">The active Options menu group container populated by RE Engine.</param>
    /// <param name="customOptionId">The custom option <see cref="Guid" /> assigned when building the option node.</param>
    /// <returns>The live <see cref="app.UIPartsOptionUnit" /> whose <c>UnitData.Setting.TypeId</c> matches.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if <paramref name="customOptionId" /> is not allocated in the current Options rebuild or is absent
    ///     from <paramref name="partsGroup" />.
    /// </exception>
    public static app.UIPartsOptionUnit GetPartsOptionUnit(this app.UIPartsGroup partsGroup, Guid customOptionId)
    {
        if (!OptionTypeRegistry.TryGetOptionType(customOptionId, out var optionType))
            throw new InvalidOperationException($"{customOptionId} has not been registered yet");

        for (var i = 0; i < partsGroup._Children.Count; i++)
        {
            var part = partsGroup._Children[i];
            var uiPart = ManagedProxy<app.UIPartsOptionUnit>.Create(part);
            if (uiPart.UnitData.Setting.TypeId == optionType) return uiPart;
        }

        throw new InvalidOperationException($"Cannot find UI Part associated Id {customOptionId}");
    }

    /// <summary>
    ///     Attempts to find the <see cref="app.UIPartsOptionUnit" /> under <paramref name="partsGroup" /> matching
    ///     <paramref name="customOptionId" /> without throwing when missing or unregistered.
    /// </summary>
    /// <param name="partsGroup">The active Options menu group container.</param>
    /// <param name="customOptionId">The custom option <see cref="Guid" /> assigned when building the option node.</param>
    /// <param name="result">When this returns <see langword="true" />, receives the matching <see cref="app.UIPartsOptionUnit" />.</param>
    /// <returns><see langword="true" /> if the option part was found in <paramref name="partsGroup" />; otherwise <see langword="false" />.</returns>
    public static bool TryGetPartsOptionUnit(this app.UIPartsGroup partsGroup, Guid customOptionId,
        out app.UIPartsOptionUnit? result)
    {
        result = null;

        if (!OptionTypeRegistry.TryGetOptionType(customOptionId, out var optionType)) return false;

        for (var i = 0; i < partsGroup._Children.Count; i++)
        {
            var part = partsGroup._Children[i];
            var uiPart = ManagedProxy<app.UIPartsOptionUnit>.Create(part);
            if (uiPart.UnitData.Setting.TypeId == optionType)
            {
                result = uiPart;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Resolves the mod-defined <see cref="Guid" /> mapped to an integer <c>TypeId</c> in the active Options
    ///     rebuild.
    /// </summary>
    /// <param name="type">Runtime integer option value or group type ID (<c>app.Option.ValueType</c>/<c>GroupType</c>).</param>
    /// <param name="optionId">When this returns <see langword="true" />, receives the registered option <see cref="Guid" />.</param>
    /// <returns><see langword="true" /> if <paramref name="type" /> belongs to a currently registered custom option; otherwise <see langword="false" />.</returns>
    public static bool TryGetOptionId(int type, out Guid optionId)
    {
        return OptionTypeRegistry.TryGetOptionId(type, out optionId);
    }
}