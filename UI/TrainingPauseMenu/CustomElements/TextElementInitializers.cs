using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

/// <summary>
///     Initializes a text-only row or top-level pause menu tab button
///     (<see cref="app.training.ItemType.TEXT_ONLY" />). Attach child rows to open a secondary page when used as a
///     tab, or pair with <c>FunctionHookRequest</c> for an actionable button row.
/// </summary>
public class RowButtonInitializer : CustomElementBase
{
    /// <summary>
    ///     Configures a Training Pause Menu button row or top-level tab header.
    /// </summary>
    /// <param name="message">Label text displayed on the tab or button row.</param>
    /// <param name="guideMessage">Help description displayed in the footer bar when focused.</param>
    /// <param name="isEnabled">Whether the tab or button is initially enabled.</param>
    public RowButtonInitializer(CustomMessage message, CustomMessage guideMessage,
        bool isEnabled = true)
    {
        Type = app.training.ItemType.TEXT_ONLY;
        MessageID = message;
        GuideMessage = guideMessage;
        IsEnabled = isEnabled;
    }
}

/// <summary>
///     Initializes a selectable option entry inside a <see cref="SpinnerInitializer" /> (<c>SPIN_ITEM</c> or
///     <c>SPIN_ITEM_GUIDE</c> when paired with an inline button prompt icon).
/// </summary>
public class SpinnerItemTextInitializer : CustomElementBase
{
    /// <summary>
    ///     Configures a spinner choice entry and automatically selects <see cref="app.training.ItemType.SPIN_ITEM_GUIDE" />
    ///     if <paramref name="inputGuide" /> is valid, or <see cref="app.training.ItemType.SPIN_ITEM" /> otherwise.
    /// </summary>
    /// <param name="message">Option text rendered inside the spinner box when this choice is selected.</param>
    /// <param name="inputGuide">
    ///     Optional inline digital prompt icon (for example <see cref="app.InputAssign.Digital.Id.UITrainingOptionY" />)
    ///     indicating a submenu or secondary action on this choice.
    /// </param>
    public SpinnerItemTextInitializer(CustomMessage message,
        app.InputAssign.Digital.Id inputGuide = app.InputAssign.Digital.Id.Invalid)
    {
        Type = inputGuide == app.InputAssign.Digital.Id.Invalid
            ? app.training.ItemType.SPIN_ITEM
            : app.training.ItemType.SPIN_ITEM_GUIDE;
        MessageID = message;
        IsEnabled = true;
        GuidIcon = inputGuide;
    }
}