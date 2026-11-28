using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.ReplayPauseMenu.CustomElements;

/// <summary>
///     Initializes a clickable button entry (<see cref="app.ReplayMenuItem.EItemType.Button" />) in SF6's Replay
///     Pause Menu.
/// </summary>
public sealed class ReplayButtonInitializer : CustomReplayItemBase
{
    public ReplayButtonInitializer(
        CustomMessage itemMessage,
        app.ReplayMenuItem.ETabType tabType = app.ReplayMenuItem.ETabType.DispSetting,
        app.ReplayMenuItem.EButtonType buttonType = app.ReplayMenuItem.EButtonType.Type0)
    {
        ItemType = app.ReplayMenuItem.EItemType.Button;
        ItemMsg = itemMessage;
        TabType = tabType;
        ButtonType = buttonType;
    }
}

/// <summary>
///     Initializes a horizontal spinner entry (<see cref="app.ReplayMenuItem.EItemType.Spin" />) in SF6's Replay
///     Pause Menu with localized <c>ChildData</c> options and an optional initial-index resolver.
/// </summary>
public class ReplaySpinnerInitializer : CustomReplayItemBase
{
    public ReplaySpinnerInitializer(
        CustomMessage itemMessage,
        List<CustomMessage> childMessages,
        app.ReplayMenuItem.ETabType tabType = app.ReplayMenuItem.ETabType.DispSetting,
        Func<int>? getInitialSpinIndex = null)
    {
        ItemType = app.ReplayMenuItem.EItemType.Spin;
        ItemMsg = itemMessage;
        ChildMessages = childMessages;
        TabType = tabType;
        GetInitialSpinIndex = getInitialSpinIndex;
    }
}

/// <summary>
///     Preset <see cref="ReplaySpinnerInitializer" /> populated with vanilla <c>Hide</c> (<c>0</c>) and <c>Show</c>
///     (<c>1</c>) option labels.
/// </summary>
public sealed class ReplayHideShowSpinnerInitializer : ReplaySpinnerInitializer
{
    public static readonly Guid HideMessageGuid = new("2a3399f4-9925-412a-a51a-bb3870bf4696");
    public static readonly Guid ShowMessageGuid = new("89a7d6b0-27c8-448a-8b0b-488701fac8db");

    public ReplayHideShowSpinnerInitializer(
        CustomMessage itemMessage,
        app.ReplayMenuItem.ETabType tabType = app.ReplayMenuItem.ETabType.DispSetting,
        Func<bool>? getIsShown = null)
        : base(
            itemMessage,
            [new CustomMessage(HideMessageGuid), new CustomMessage(ShowMessageGuid)],
            tabType,
            getIsShown != null ? () => getIsShown() ? 1 : 0 : null)
    {
    }
}

