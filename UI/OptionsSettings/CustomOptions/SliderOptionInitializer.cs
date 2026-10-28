using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

public class SliderOptionInitializer : CustomOptionValue
{
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