using SF6_Plugin_Core.UI.OptionsSettings.Core;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

public abstract class CustomOptionValue : CustomOptionBase, IUICustomOptionsValueElementInitialization
{
    protected ManagedObject? _valueDataMo;
    protected ManagedObject? _valueSettingsMo;
    protected int MaxValue { get; init; }
    protected int MinValue { get; init; }

    /// <summary>
    ///     This is the "initial" value as in the default value, and what it will go back to when you press the "Reset to
    ///     Defaults" button
    /// </summary>
    protected int InitValue { get; init; }

    /// <summary>
    ///     Use it as a way to initialize the current value based on your own config or similar
    /// </summary>
    protected int CurrentValue { get; init; }

    protected override app.Option.SettingDataType SettingDataType { get; init; } = app.Option.SettingDataType.Value;

    public override app.Option.OptionValueUnit InitializeCustomElement(Guid functionNameId)
    {
        EnsureMessageRegistration();

        if (_customOptionDataMo != null && _settingsUnitMo != null && _valueDataMo != null && _valueSettingsMo != null)
        {
            var existingValue = _customOptionDataMo.As<app.Option.OptionValueUnit>();
            var regType = OptionTypeRegistry.RegisterNewOptionType(functionNameId);

            var settingsUnit2 = _settingsUnitMo.As<app.Option.OptionSettingUnit>();
            settingsUnit2.ValueType = (app.Option.ValueType)regType;

            var valueData2 = _valueDataMo.As<app.Option.OptionValueData>();
            valueData2.TypeId = regType;

            var valueSettings2 = _valueSettingsMo.As<app.Option.OptionValueSetting>();
            valueSettings2.TypeId = regType;

            existingValue.Setting = settingsUnit2;
            existingValue.ValueData = valueData2;
            existingValue.ValueSetting = valueSettings2;

            return existingValue;
        }

        var newValueType = OptionTypeRegistry.RegisterNewOptionType(functionNameId);

        var newOptionDataMo = app.Option.OptionValueUnit.REFType.CreateInstance(0);
        newOptionDataMo.Globalize();
        _customOptionDataMo = newOptionDataMo;


        var newSettingsMo = app.Option.OptionSettingUnit.REFType.CreateInstance(0);
        newSettingsMo.Globalize();
        _settingsUnitMo = newSettingsMo;

        InitializeUnitBase();

        var settingsUnit = _settingsUnitMo.As<app.Option.OptionSettingUnit>();

        settingsUnit.ValueType = (app.Option.ValueType)newValueType;

        var newOptionData = _customOptionDataMo.As<app.Option.OptionValueUnit>();

        _valueDataMo = app.Option.OptionValueData.REFType.CreateInstance(0);
        _valueDataMo.Globalize();

        var valueData = _valueDataMo.As<app.Option.OptionValueData>();
        valueData.TypeId = newValueType;
        valueData.Value = CurrentValue;

        _valueSettingsMo = app.Option.OptionValueSetting.REFType.CreateInstance(0);
        _valueSettingsMo.Globalize();

        var valueSettings = _valueSettingsMo.As<app.Option.OptionValueSetting>();
        valueSettings.TypeId = newValueType;
        valueSettings.MaxValue = MaxValue;
        valueSettings.MinValue = MinValue;
        valueSettings.InitValue = InitValue;

        newOptionData.ValueData = valueData;
        newOptionData.ValueSetting = valueSettings;

        return newOptionData;
    }

    public override app.Option.OptionValueUnit GetElementData()
    {
        return _customOptionDataMo?.As<app.Option.OptionValueUnit>() ??
               throw new InvalidOperationException("Custom element data has not been initialized.");
    }

    public override void Clear()
    {
        base.Clear();
        _valueDataMo?.ReleaseIfGlobalized();
        _valueDataMo = null;
        _valueSettingsMo?.ReleaseIfGlobalized();
        _valueSettingsMo = null;
    }
}