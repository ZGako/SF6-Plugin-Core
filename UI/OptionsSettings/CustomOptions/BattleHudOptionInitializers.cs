using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

public class OpenBattleHudOptionInitializer : CustomOptionGroup
{
    public OpenBattleHudOptionInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
        UnitInputType = app.Option.UnitInputType.Button_Type0;
        SupportModeType = (app.Option.SupportModeType)55; // everything but EMULATOR (I assume this is the arcade thing)
        DecideEventType =  app.Option.DecideEventType.OpenBattleHudSetting;
    }
}

public class OpenBattleHudOptionSubmenuInitializer : CustomOptionGroup
{
    /// <summary>
    /// Initializer for a group node which opens a submenu upon button press.
    /// </summary>
    /// <param name="titleMessage"></param>
    /// <param name="descriptionMessage"></param>
    public OpenBattleHudOptionSubmenuInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
        UnitInputType = app.Option.UnitInputType.Button_Type2;
        SupportModeType = (app.Option.SupportModeType)55; // everything but EMULATOR (I assume this is the arcade thing)
        DecideEventType =  app.Option.DecideEventType.OpenBattleHudCustomSetting;
    }
}

public class BattleHudSimpleButtonInitializer : CustomOptionGroup
{
    /// <summary>
    /// Initializer for a group node which opens a submenu upon button press.
    /// </summary>
    /// <param name="titleMessage"></param>
    /// <param name="descriptionMessage"></param>
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