using SF6_Plugin_Core.UI.Localization;
namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

public class RowButtonInitializer : CustomElementBase
{
    public RowButtonInitializer(CustomMessage message, CustomMessage guideMessage)
    {
        Type = app.training.ItemType.TEXT_ONLY;
        MessageID = message;
        GuideMessage = guideMessage;
        IsEnabled = true;
    }
}

public class SpinnerOptionTextInitializer : CustomElementBase
{
    public SpinnerOptionTextInitializer(CustomMessage message)
    {
        Type = app.training.ItemType.TEXT_ONLY;
        MessageID = message;
        IsEnabled = true;
    }
}