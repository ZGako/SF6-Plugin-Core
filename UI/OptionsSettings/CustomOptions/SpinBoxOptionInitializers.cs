using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

/// <summary>
///     Initializes a discrete text spinner (<see cref="app.Option.UnitInputType.SpinText" />) whose selectable
///     labels map to 0-based integer indices <c>0..valueMessages.Count - 1</c>.
/// </summary>
public class SpinBoxOptionInitializer : CustomOptionValue
{
    /// <summary>
    ///     Creates a localized spin-box option from an ordered list of choice labels. Logs a warning if
    ///     <paramref name="defaultValue" /> or <paramref name="currentStartingValue" /> falls outside
    ///     <c>0..valueMessages.Count - 1</c>.
    /// </summary>
    /// <param name="titleMessage">Row label displayed next to the spinner.</param>
    /// <param name="descriptionMessage">Description shown at the bottom when focused.</param>
    /// <param name="valueMessages">Ordered list of choice labels indexed <c>0..Count - 1</c> in value events.</param>
    /// <param name="currentStartingValue">0-based index selected when the option is first built.</param>
    /// <param name="defaultValue">0-based index restored on "Reset to Defaults" (defaults to <c>0</c>).</param>
    public SpinBoxOptionInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage,
        List<CustomMessage> valueMessages,
        int currentStartingValue, int defaultValue = 0)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
        UnitInputType = app.Option.UnitInputType.SpinText;
        ValueMessageList.AddRange(valueMessages);
        if (valueMessages.Count > 0)
        {
            MaxValue = valueMessages.Count - 1;

            if (defaultValue >= MinValue && defaultValue <= MaxValue)
                InitValue = defaultValue;
            else
                API.LogWarning("Default value is outside the boundaries set by Min and Max values");

            if (currentStartingValue >= MinValue && currentStartingValue <= MaxValue)
                CurrentValue = currentStartingValue;
            else
                API.LogWarning("Starting value is outside the boundaries set by Min and Max values");
        }
    }
}

/// <summary>
///     Initializes a built-in On/Off toggle spinner (<see cref="app.Option.UnitInputType.SpinText_OnOff" />).
///     Note that SF6 encodes <c>0</c> as On and <c>1</c> as Off in value-change callbacks; this initializer converts
///     boolean inputs accordingly.
/// </summary>
public class OnOffSpinBoxOptionInitializer : CustomOptionValue
{
    /// <summary>
    ///     Creates an On/Off option spinner mapping <paramref name="isStartEnabled" /> and
    ///     <paramref name="defaultValue" /> to SF6's inverted convention (<c>0 = On</c>, <c>1 = Off</c>).
    /// </summary>
    /// <param name="titleMessage">Row label displayed next to the toggle.</param>
    /// <param name="descriptionMessage">Description shown at the bottom when focused.</param>
    /// <param name="isStartEnabled">Whether the toggle starts in the On (<c>0</c>) state when built.</param>
    /// <param name="defaultValue">Whether "Reset to Defaults" resets the toggle to On (<c>true</c>) or Off (<c>false</c>).</param>
    public OnOffSpinBoxOptionInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage,
        bool isStartEnabled, bool defaultValue = false)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
        UnitInputType = app.Option.UnitInputType.SpinText_OnOff;
        // inverted as the game has 0 as "On" and 1 as "Off" in the spin box options
        CurrentValue = isStartEnabled ? 0 : 1;
        InitValue = defaultValue ? 0 : 1;
    }
}

/// <summary>
///     Doesn't work it seems
/// </summary>
internal class NumSpinBoxOptionInitializer : CustomOptionValue
{
    public NumSpinBoxOptionInitializer(
        CustomMessage titleMessage,
        CustomMessage descriptionMessage,
        int minValue,
        int maxValue,
        int currentStartingValue,
        int defaultValue = 0)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
        UnitInputType = app.Option.UnitInputType.SpinText_Num;

        MinValue = minValue;
        MaxValue = maxValue;

        if (defaultValue >= MinValue && defaultValue <= MaxValue)
            InitValue = defaultValue;
        else
            API.LogWarning("Default value is outside the boundaries set by Min and Max values");

        if (currentStartingValue >= MinValue && currentStartingValue <= MaxValue)
            CurrentValue = currentStartingValue;
        else
            API.LogWarning("Starting value is outside the boundaries set by Min and Max values");
    }
}