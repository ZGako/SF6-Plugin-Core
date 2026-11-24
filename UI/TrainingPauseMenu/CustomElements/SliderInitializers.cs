using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

public class SliderInitializer : CustomElementBase
{
    public SliderInitializer(CustomMessage message, CustomMessage guideMessage,
        (int, int) limitValue,
        int interval,
        int intervalOption,
        bool isEnabled = true,
        CustomMessage? endWordsGuid = null)
    {
        Type = app.training.ItemType.SLIDER;
        MessageID = message;
        GuideMessage = guideMessage;
        IsEnabled = isEnabled;
        LimitValue = limitValue;
        Interval = interval;
        Interval_Option = intervalOption;
        EndWordsGuid = endWordsGuid;
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
        bool isEnabled = true,
        CustomMessage? endWordsGuid = null,
        int intervalOption = 1)
    {
        Type = app.training.ItemType.SLIDER_VITAL_1P;
        MessageID = message;
        GuideMessage = guideMessage;
        IsEnabled = isEnabled;
        LimitValue = limitValue;
        EndWordsGuid = endWordsGuid;
        Interval_Option = intervalOption;
    }
}