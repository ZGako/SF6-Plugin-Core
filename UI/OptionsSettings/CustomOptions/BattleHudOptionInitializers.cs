using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

/// <summary>
///     Initializes a button row (<see cref="app.Option.DecideEventType.OpenBattleHudSetting" />) in the Options menu
///     that launches SF6's Battle HUD configuration flow across standard console/PC support modes.
/// </summary>
public class OpenBattleHudOptionInitializer : CustomOptionGroup
{
    /// <summary>
    ///     Creates an Options row configured to invoke <see cref="app.Option.DecideEventType.OpenBattleHudSetting" />.
    /// </summary>
    /// <param name="titleMessage">Row label displayed on the option entry.</param>
    /// <param name="descriptionMessage">Description shown when the row is focused.</param>
    public OpenBattleHudOptionInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
        UnitInputType = app.Option.UnitInputType.Button_Type0;
        SupportModeType = (app.Option.SupportModeType)55; // everything but EMULATOR (I assume this is the arcade thing)
        DecideEventType =  app.Option.DecideEventType.OpenBattleHudSetting;
    }
}

/// <summary>
///     Initializes a secondary button row (<see cref="app.Option.UnitInputType.Button_Type2" />,
///     <see cref="app.Option.DecideEventType.OpenBattleHudCustomSetting" />) opening SF6's custom HUD layout screen.
/// </summary>
public class OpenBattleHudOptionSubmenuInitializer : CustomOptionGroup
{
    /// <summary>
    ///     Creates a centered button row configured to open the Battle HUD custom setting submenu.
    /// </summary>
    /// <param name="titleMessage">Button label displayed on the option entry.</param>
    /// <param name="descriptionMessage">Description shown when the row is focused.</param>
    public OpenBattleHudOptionSubmenuInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
        UnitInputType = app.Option.UnitInputType.Button_Type2;
        SupportModeType = (app.Option.SupportModeType)55; // everything but EMULATOR (I assume this is the arcade thing)
        DecideEventType =  app.Option.DecideEventType.OpenBattleHudCustomSetting;
    }
}

/// <summary>
///     Initializes a button row (<see cref="app.Option.DecideEventType.OpenBattleHudOffsetSetting" />) that opens
///     SF6's Battle HUD screen-offset adjustment screen.
/// </summary>
public class BattleHudSimpleButtonInitializer : CustomOptionGroup
{
    /// <summary>
    ///     Creates a button row configured to open the Battle HUD offset settings screen.
    /// </summary>
    /// <param name="titleMessage">Button label displayed on the option entry.</param>
    /// <param name="descriptionMessage">Description shown when the row is focused.</param>
    public BattleHudSimpleButtonInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
        UnitInputType = app.Option.UnitInputType.Button_Type2;
        SupportModeType = (app.Option.SupportModeType)55; // everything but EMULATOR (I assume this is the arcade thing)
        // TODO: this is a test to see if this will 
        DecideEventType =  app.Option.DecideEventType.OpenBattleHudOffsetSetting;
    }
}