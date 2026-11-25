using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

/// <summary>
///     Initializes a continuous integer slider option row (<see cref="app.Option.UnitInputType.Slider" />) for
///     SF6's Options menu. Pair with <c>OnValueTypeEventHookRequest</c> on its <see cref="UICustomOptionsValueNode" />
///     to observe user adjustments and resets.
/// </summary>
public class SliderOptionInitializer : CustomOptionValue
{
    /// <summary>
    ///     Configures an Options slider with explicit bounds, initial value, and reset-to-defaults target.
    /// </summary>
    /// <param name="titleMessage">Row label displayed on the left of the slider.</param>
    /// <param name="descriptionMessage">Description shown at the bottom when the slider row is focused.</param>
    /// <param name="minValue">Inclusive minimum slider integer.</param>
    /// <param name="maxValue">Inclusive maximum slider integer.</param>
    /// <param name="startingValue">Initial value displayed when the Options UI is built.</param>
    /// <param name="defaultValue">Fallback value applied when the user resets Options settings to defaults.</param>
    public SliderOptionInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage,
        int minValue, int maxValue, int startingValue, int defaultValue)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
        UnitInputType = app.Option.UnitInputType.Slider;
        MinValue = minValue;
        MaxValue = maxValue;
        InitValue = defaultValue;
        CurrentValue = startingValue;
    }
}