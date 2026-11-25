using SF6_Plugin_Core.UI.OptionsSettings.Core;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

/// <summary>
///     Abstract initializer for custom <see cref="app.Option.OptionValueUnit" /> interactive rows (sliders, text
///     spinners, on/off toggles, and non-numeric labels), allocating and pinning both <c>OptionValueData</c> and
///     <c>OptionValueSetting</c>.
/// </summary>
public abstract class CustomOptionValue : CustomOptionBase, IUICustomOptionsValueElementInitialization
{
    /// <summary>Globalized native <see cref="app.Option.OptionValueData" /> holding the option's live integer value and <c>TypeId</c>.</summary>
    protected ManagedObject? _valueDataMo;

    /// <summary>Globalized native <see cref="app.Option.OptionValueSetting" /> holding <c>MinValue</c>, <c>MaxValue</c>, and <c>InitValue</c>.</summary>
    protected ManagedObject? _valueSettingsMo;

    /// <summary>Inclusive upper bound enforced by the Options UI widget.</summary>
    protected int MaxValue { get; init; }

    /// <summary>Inclusive lower bound enforced by the Options UI widget.</summary>
    protected int MinValue { get; init; }

    /// <summary>
    ///     Default integer value written to <c>OptionValueSetting.InitValue</c>, restored when the user presses
    ///     SF6's "Reset to Defaults" action in the Options menu.
    /// </summary>
    protected int InitValue { get; init; }

    /// <summary>
    ///     Starting integer value assigned to <c>OptionValueData.Value</c> when the native option unit is first
    ///     constructed (typically loaded from the mod's config).
    /// </summary>
    protected int CurrentValue { get; init; }

    /// <inheritdoc />
    protected override app.Option.SettingDataType SettingDataType { get; init; } = app.Option.SettingDataType.Value;

    /// <inheritdoc />
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

    /// <inheritdoc />
    public override app.Option.OptionValueUnit GetElementData()
    {
        return _customOptionDataMo?.As<app.Option.OptionValueUnit>() ??
               throw new InvalidOperationException("Custom element data has not been initialized.");
    }

    /// <summary>
    ///     Releases base native objects/messages as well as the extra <see cref="_valueDataMo" /> and
    ///     <see cref="_valueSettingsMo" /> handles.
    /// </summary>
    public override void Clear()
    {
        base.Clear();
        _valueDataMo?.ReleaseIfGlobalized();
        _valueDataMo = null;
        _valueSettingsMo?.ReleaseIfGlobalized();
        _valueSettingsMo = null;
    }
}