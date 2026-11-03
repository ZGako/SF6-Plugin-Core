using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

public class SliderInitializer : CustomElementBase
{
    public SliderInitializer(CustomMessage message, CustomMessage guideMessage,
        (int, int) limitValue,
        int interval,
        int intervalOption)
    {
        Type = app.training.ItemType.SLIDER;
        MessageID = message;
        GuideMessage = guideMessage;
        IsEnabled = true;
        LimitValue = limitValue;
        Interval = interval;
        Interval_Option = intervalOption;
    }
}