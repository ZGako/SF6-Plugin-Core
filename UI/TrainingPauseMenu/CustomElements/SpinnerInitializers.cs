using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

public class SubMenuSpinnerInitializer : CustomElementBase
{
    public SubMenuSpinnerInitializer(CustomMessage message, CustomMessage guideMessage, (int, int) limitValue)
    {
        Type = app.training.ItemType.SPIN;
        MessageID = message;
        IsEnabled = true;
        LimitValue = limitValue;
        GuideMessage = guideMessage;
        EndWordsGuid = new CustomMessage(Guid.Parse("db099697-8e5b-4bac-9d89-4d4b971bb631"));
    }
}