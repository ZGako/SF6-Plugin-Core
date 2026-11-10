using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

public class RowButtonInitializer : CustomElementBase
{
    public RowButtonInitializer(CustomMessage message, CustomMessage guideMessage,
        bool isEnabled = true)
    {
        Type = app.training.ItemType.TEXT_ONLY;
        MessageID = message;
        GuideMessage = guideMessage;
        IsEnabled = isEnabled;
    }
}

public class SpinnerItemTextInitializer : CustomElementBase
{
    public SpinnerItemTextInitializer(CustomMessage message,
        app.InputAssign.Digital.Id inputGuide = app.InputAssign.Digital.Id.Invalid)
    {
        Type = app.training.ItemType.TEXT_ONLY;
        MessageID = message;
        IsEnabled = true;
        GuidIcon = inputGuide;
    }
}