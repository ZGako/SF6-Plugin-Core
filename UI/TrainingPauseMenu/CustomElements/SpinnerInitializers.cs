using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

public class SubMenuSpinnerInitializer : CustomElementBase
{
    public SubMenuSpinnerInitializer(CustomMessage message, CustomMessage guideMessage, (int, int) limitValue,
        bool isEnabled = true)
    {
        Type = app.training.ItemType.SPIN;
        MessageID = message;
        IsEnabled = isEnabled;
        LimitValue = limitValue;
        GuideMessage = guideMessage;
    }
}

public class SpinnerInitializer : CustomElementBase
{
    public SpinnerInitializer(CustomMessage message, CustomMessage guideMessage,
        bool isEnabled = true)
    {
        Type = app.training.ItemType.SPIN;
        MessageID = message;
        IsEnabled = isEnabled;
        GuideMessage = guideMessage;
    }
}