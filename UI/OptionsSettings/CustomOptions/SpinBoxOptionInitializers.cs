using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

public class SpinBoxOptionInitializer : CustomOptionValue
{
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

            if (currentStartingValue >= MinValue && CurrentValue <= currentStartingValue)
                CurrentValue = currentStartingValue;
            else
                API.LogWarning("Starting value is outside the boundaries set by Min and Max values");
        }
    }
}

public class OnOffSpinBoxOptionInitializer : CustomOptionValue
{
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