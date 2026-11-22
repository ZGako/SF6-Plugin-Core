using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

public class SliderInitializer : CustomElementBase
{
    public SliderInitializer(CustomMessage message, CustomMessage guideMessage,
        (int, int) limitValue,
        int interval,
        int intervalOption,
        bool isEnabled = true)
    {
        Type = app.training.ItemType.SLIDER;
        MessageID = message;
        GuideMessage = guideMessage;
        IsEnabled = isEnabled;
        LimitValue = limitValue;
        Interval = interval;
        Interval_Option = intervalOption;
    }
}

public class DriveSliderInitializer : CustomElementBase
{
    public DriveSliderInitializer(CustomMessage message, CustomMessage guideMessage,
        (int, int) limitValue,
        bool isEnabled = true)
    {
        Type = app.training.ItemType.SLIDER_DRIVE;
        MessageID = message;
        GuideMessage = guideMessage;
        IsEnabled = isEnabled;
        LimitValue = limitValue;
        GuidIcon = app.InputAssign.Digital.Id.UITrainingOptionY;
    }
}

public class VitalSliderInitializer : CustomElementBase
{
    public VitalSliderInitializer(CustomMessage message, CustomMessage guideMessage,
        (int, int) limitValue,
        bool isEnabled = true)
    {
        Type = app.training.ItemType.SLIDER_VITAL_1P;
        MessageID = message;
        GuideMessage = guideMessage;
        IsEnabled = isEnabled;
        LimitValue = limitValue;
        EndWordsGuid = new CustomMessage(Guid.Parse("86bf146b-7aa4-447c-8a20-35e4cb943b7d"));
    }
}