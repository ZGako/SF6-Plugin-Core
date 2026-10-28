using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

public class TextOptionInitializer : CustomOptionValue
{
    public TextOptionInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
    }
}

public class TextOptionGroupInitializer : CustomOptionGroup
{
    /// <summary>
    /// DOES NOT WORK
    /// Used for "leaf" nodes that are buttons only (for anything else, use a value node instead)
    /// </summary>
    /// <param name="titleMessage"></param>
    /// <param name="descriptionMessage"></param>
    /// <param name="centerText"></param>
    public TextOptionGroupInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage, bool centerText = false)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
        UnitInputType = centerText ? app.Option.UnitInputType.Button_Type2 : app.Option.UnitInputType.Button_Type0;
        SupportModeType = (app.Option.SupportModeType)55; // everything but EMULATOR (I assume this is the arcade thing)
        DecideEventType = app.Option.DecideEventType.Invalid;
    }
}

public class GroupRowButtonInitializer : CustomOptionGroup
{
    /// <summary>
    /// Initializer for a group node which opens a submenu upon button press.
    /// </summary>
    /// <param name="titleMessage"></param>
    /// <param name="descriptionMessage"></param>
    /// <param name="centerText"></param>
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