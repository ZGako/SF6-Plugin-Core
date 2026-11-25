using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

/// <summary>
///     Initializes a generic integer slider row (<see cref="app.training.ItemType.SLIDER" />) for the Training Pause
///     Menu.
/// </summary>
public class SliderInitializer : CustomElementBase
{
    /// <summary>
    ///     Configures a Training Pause Menu slider with explicit range, step intervals, and optional suffix text.
    /// </summary>
    /// <param name="message">Row label displayed on the left of the slider.</param>
    /// <param name="guideMessage">Help text displayed in the bottom guide bar when focused.</param>
    /// <param name="limitValue">Inclusive <c>(Min, Max)</c> integer bounds for the slider.</param>
    /// <param name="interval">Standard step size per directional press.</param>
    /// <param name="intervalOption">Optional accelerated step size stored in <c>_Interval_Option</c>.</param>
    /// <param name="isEnabled">Whether the slider row starts interactive.</param>
    /// <param name="endWordsGuid">Optional unit/suffix message (such as <c>"%"</c>) appended to the displayed value.</param>
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

/// <summary>
///     Initializes a Drive-gauge styled submenu slider (<see cref="app.training.ItemType.SLIDER_DRIVE" />) with the
///     <see cref="app.InputAssign.Digital.Id.UITrainingOptionY" /> icon preset for P1/P2 Drive Gauge submenus.
/// </summary>
public class DriveSliderInitializer : CustomElementBase
{
    /// <summary>
    ///     Configures a Drive-gauge submenu slider with inclusive bounds and default Option Y prompt icon.
    /// </summary>
    /// <param name="message">Row label displayed next to the Drive gauge slider.</param>
    /// <param name="guideMessage">Help text displayed in the bottom guide bar when focused.</param>
    /// <param name="limitValue">Inclusive <c>(Min, Max)</c> gauge bounds.</param>
    /// <param name="isEnabled">Whether the slider row starts interactive.</param>
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

/// <summary>
///     Initializes a Vitality/health-bar styled slider (<see cref="app.training.ItemType.SLIDER_VITAL_1P" />), used
///     for Health-like segmented bars or custom character gauge timers in the Training Pause Menu.
/// </summary>
public class VitalSliderInitializer : CustomElementBase
{
    /// <summary>
    ///     Configures a Vital-styled slider with explicit range, step option, optional suffix, and inline button icon.
    /// </summary>
    /// <param name="message">Row label displayed next to the slider.</param>
    /// <param name="guideMessage">Help text displayed in the bottom guide bar when focused.</param>
    /// <param name="limitValue">Inclusive <c>(Min, Max)</c> integer bounds.</param>
    /// <param name="isEnabled">Whether the slider row starts interactive.</param>
    /// <param name="endWordsGuid">Optional unit/suffix message (such as <c>"%"</c>) displayed after the numeric value.</param>
    /// <param name="intervalOption">Step increment stored in <c>_Interval_Option</c> (defaults to <c>1</c>).</param>
    /// <param name="guidIcon">Optional inline button prompt icon (defaults to <see cref="app.InputAssign.Digital.Id.Invalid" />).</param>
    public VitalSliderInitializer(CustomMessage message, CustomMessage guideMessage,
        (int, int) limitValue,
        bool isEnabled = true,
        CustomMessage? endWordsGuid = null,
        int intervalOption = 1,
        app.InputAssign.Digital.Id guidIcon = app.InputAssign.Digital.Id.Invalid)
    {
        Type = app.training.ItemType.SLIDER_VITAL_1P;
        MessageID = message;
        GuideMessage = guideMessage;
        IsEnabled = isEnabled;
        LimitValue = limitValue;
        EndWordsGuid = endWordsGuid;
        Interval_Option = intervalOption;
        GuidIcon = guidIcon;
    }
}