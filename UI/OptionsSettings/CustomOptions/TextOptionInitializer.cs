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
    public TextOptionGroupInitializer(CustomMessage titleMessage, CustomMessage descriptionMessage)
    {
        TitleMessage = titleMessage;
        DescriptionMessage = descriptionMessage;
        UnitInputType = app.Option.UnitInputType.Button_Type1;
    }
}