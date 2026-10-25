using SF6_Plugin_Core.UI.OptionsSettings.Core;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

public abstract class CustomOptionGroup : CustomOptionBase, IUICustomOptionsGroupElementInitialization
{
    // the only field here is the child list, which we actually handle in the RequestNode

    protected override app.Option.SettingDataType SettingDataType { get; init; } = app.Option.SettingDataType.Group;

    public override app.Option.OptionGroupUnit InitializeCustomElement(Guid functionNameId)
    {
        EnsureMessageRegistration();

        if (_customOptionDataMo != null)
        {
            var existingGroup = _customOptionDataMo.As<app.Option.OptionGroupUnit>();
            existingGroup.Setting.GroupType =
                (app.Option.GroupType)OptionTypeRegistry.RegisterNewOptionType(functionNameId);
            return existingGroup;
        }

        var newGroupType = OptionTypeRegistry.RegisterNewOptionType(functionNameId);

        var newOptionDataMo = app.Option.OptionGroupUnit.REFType.CreateInstance(0);
        newOptionDataMo.Globalize();
        _customOptionDataMo = newOptionDataMo;


        var newSettingsMo = app.Option.OptionSettingUnit.REFType.CreateInstance(0);
        newSettingsMo.Globalize();
        _settingsUnitMo = newSettingsMo;

        InitializeUnitBase();

        // TODO: do this thing

        var settingsUnit = _settingsUnitMo.As<app.Option.OptionSettingUnit>();

        // TODO: do this thing
        settingsUnit.GroupType = (app.Option.GroupType)newGroupType;

        var newOptionData = _customOptionDataMo.As<app.Option.OptionGroupUnit>();

        return newOptionData;
    }

    public override app.Option.OptionGroupUnit GetElementData()
    {
        return _customOptionDataMo?.As<app.Option.OptionGroupUnit>() ??
               throw new InvalidOperationException("Custom element data has not been initialized.");
    }
}