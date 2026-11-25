using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

/// <summary>
///     Initializes a spin-list submenu spinner row (<see cref="app.training.ItemType.SPIN" />) whose display text,
///     current value, and dynamic bounds are driven by <c>SubMenuSpinnerHookRequest</c> inside
///     <c>UIFlowTrainingMenu_SpinList</c> popups rather than static <c>ChildNodes</c>.
/// </summary>
public class SubMenuSpinnerInitializer : CustomElementBase
{
    /// <summary>
    ///     Configures a submenu numeric/formatted spinner with static fallback <paramref name="limitValue" /> bounds.
    /// </summary>
    /// <param name="message">Row label inside the <c>SPINLIST</c> popup.</param>
    /// <param name="guideMessage">Help text displayed at the bottom when focused.</param>
    /// <param name="limitValue">
    ///     Inclusive <c>(Min, Max)</c> value range (may be overridden dynamically per open/refresh by
    ///     <c>SubMenuSpinnerHook.SubMenuSpinnerLimitDelegate</c>).
    /// </param>
    /// <param name="isEnabled">Whether the submenu spinner row starts enabled.</param>
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

/// <summary>
///     Initializes a standard Training Pause Menu spinner row (<see cref="app.training.ItemType.SPIN" />) populated
///     from child <see cref="SpinnerItemTextInitializer" /> nodes and synchronized via <c>SpinBoxHookRequest</c>.
/// </summary>
public class SpinnerInitializer : CustomElementBase
{
    /// <summary>
    ///     Configures a Training Pause Menu spinner row whose selectable options are supplied as child nodes on its
    ///     <see cref="UICustomElementRequestNode" />.
    /// </summary>
    /// <param name="message">Row label displayed on the left of the spinner.</param>
    /// <param name="guideMessage">Help text displayed in the bottom guide bar when focused.</param>
    /// <param name="isEnabled">Whether the spinner starts enabled for cursor focus and input.</param>
    public SpinnerInitializer(CustomMessage message, CustomMessage guideMessage,
        bool isEnabled = true)
    {
        Type = app.training.ItemType.SPIN;
        MessageID = message;
        IsEnabled = isEnabled;
        GuideMessage = guideMessage;
    }
}