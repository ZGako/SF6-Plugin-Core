using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

/// <summary>
///     Initializes a static button-style row backed by <see cref="CustomOptionValue" /> (using default
///     <see cref="app.Option.UnitInputType.Button_Type0" />) for non-interactive labels or info rows in Options menus.
/// </summary>
public class TextOptionInitializer : CustomOptionValue
{
    /// <summary>
    ///     Creates a static value-backed text/button entry in the Options menu.
    /// </summary>
    /// <param name="titleMessage">Row label text.</param>
    /// <param name="descriptionMessage">Description displayed at the bottom when focused.</param>
    public TextOptionInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
    }
}

/// <summary>
///     Experimental group-backed text button initializer with <see cref="app.Option.DecideEventType.Invalid" />.
/// </summary>
/// <remarks>
///     <b>Known issue:</b> Currently does not render/function as intended in SF6; use <see cref="TextOptionInitializer" />
///     on a <see cref="UICustomOptionsValueNode" /> for leaf rows instead.
/// </remarks>
public class TextOptionGroupInitializer : CustomOptionGroup
{
    /// <summary>
    ///     Configures a leaf group node with <see cref="app.Option.DecideEventType.Invalid" /> (non-functional in-game).
    /// </summary>
    /// <param name="titleMessage">Row label text.</param>
    /// <param name="descriptionMessage">Description displayed at the bottom when focused.</param>
    /// <param name="centerText">
    ///     When <see langword="true" />, selects <see cref="app.Option.UnitInputType.Button_Type2" />; otherwise
    ///     <see cref="app.Option.UnitInputType.Button_Type0" />.
    /// </param>
    public TextOptionGroupInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage, bool centerText = false)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
        UnitInputType = centerText ? app.Option.UnitInputType.Button_Type2 : app.Option.UnitInputType.Button_Type0;
        SupportModeType = (app.Option.SupportModeType)55; // everything but EMULATOR (I assume this is the arcade thing)
        DecideEventType = app.Option.DecideEventType.Invalid;
    }
}

/// <summary>
///     Initializes an <see cref="app.Option.OptionGroupUnit" /> row (<see cref="app.Option.DecideEventType.OpenSubMenu" />)
///     that opens a nested Options page populated with the node's child option units.
/// </summary>
public class GroupRowButtonInitializer : CustomOptionGroup
{
    /// <summary>
    ///     Creates a submenu-opening group row active across console/PC support modes.
    /// </summary>
    /// <param name="titleMessage">Row label text.</param>
    /// <param name="descriptionMessage">Description displayed at the bottom when focused.</param>
    /// <param name="centerText">
    ///     When <see langword="true" />, selects centered <see cref="app.Option.UnitInputType.Button_Type2" />; otherwise
    ///     left-aligned <see cref="app.Option.UnitInputType.Button_Type0" />.
    /// </param>
    public GroupRowButtonInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage, 
        bool centerText = false)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
        UnitInputType = centerText ? app.Option.UnitInputType.Button_Type2 : app.Option.UnitInputType.Button_Type0;
        SupportModeType = (app.Option.SupportModeType)55; // everything but EMULATOR (I assume this is the arcade thing)
        DecideEventType =  app.Option.DecideEventType.OpenSubMenu;
    }
}