using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

public abstract class CustomOptionBase : IUICustomOptionsElementInitialization
{
    protected readonly List<CustomMessage> ValueMessageList = [];
    protected CustomMessage? DescriptionMessage;
    protected CustomMessage? TitleMessage;
    protected ManagedObject? _customOptionDataMo;
    protected ManagedObject? _settingsUnitMo;
    protected bool isOnline;

    // prevents outside packages from deriving from the base class directly
    internal CustomOptionBase()
    {
    }

    protected abstract app.Option.SettingDataType SettingDataType { get; init; }
    protected app.Option.SupportModeType SupportModeType { get; init; } = app.Option.SupportModeType.All;
    protected app.Option.SupportPlatform SupportPlatform { get; init; } = app.Option.SupportPlatform.All;
    protected app.Option.UnitInputType UnitInputType { get; init; } = app.Option.UnitInputType.Button_Type0;
    protected app.Option.ValueType ValueType { get; init; } = app.Option.ValueType.Invalid;
    protected app.Option.GroupType GroupType { get; init; } = app.Option.GroupType.Invalid;
    protected app.Option.DecideEventType DecideEventType { get; init; } = app.Option.DecideEventType.Invalid;
    

    protected app.Option.ExtraButtonEventType ExtraButtonEventType { get; init; } =
        app.Option.ExtraButtonEventType.Invalid;

    public abstract app.Option.OptionUnitBase InitializeCustomElement(Guid functionNameId);

    public abstract app.Option.OptionUnitBase GetElementData();

    public virtual void Clear()
    {
        DescriptionMessage?.Dispose();
        TitleMessage?.Dispose();

        foreach (var message in ValueMessageList) message.Dispose();
        ValueMessageList.Clear();

        _customOptionDataMo?.ReleaseIfGlobalized();
        _customOptionDataMo = null;

        _settingsUnitMo?.ReleaseIfGlobalized();
        _settingsUnitMo = null;
    }

    protected void EnsureMessageRegistration()
    {
        TitleMessage?.EnsureRegistration();
        DescriptionMessage?.EnsureRegistration();
        foreach (var message in ValueMessageList) message.EnsureRegistration();
    }

    protected void InitializeUnitBase()
    {
        if (_customOptionDataMo == null)
            throw new InvalidOperationException("_customOptionDataMo has not been initialized.");

        var newOptionData = _customOptionDataMo.As<app.Option.OptionUnitBase>();

        if (_settingsUnitMo == null)
            throw new InvalidOperationException("_settingsUnitMo has not been initialized.");

        var settingsUnit = _settingsUnitMo.As<app.Option.OptionSettingUnit>();

        settingsUnit.DataType = SettingDataType;
        settingsUnit.InputType = UnitInputType;
        settingsUnit.SupportModeType = SupportModeType;
        settingsUnit.SupportPlatform = SupportPlatform;
        settingsUnit.IsOnlineSetting = isOnline;
        settingsUnit.EventType = DecideEventType;
        settingsUnit.ExtraEventType = ExtraButtonEventType;

        if (TitleMessage != null) _settingsUnitMo.SetGuidField("TitleMessage", TitleMessage.Id);
        if (DescriptionMessage != null) _settingsUnitMo.SetGuidField("DescriptionMessage", DescriptionMessage.Id);

        // TODO: the value message lists
        foreach (var message in ValueMessageList)
        {
            _System.Guid newEngineGuid = _System.Guid.Parse(message.Id.ToString());
            settingsUnit.ValueMessageList.Add(newEngineGuid);
        }

        settingsUnit.ChangeValueSETriggerId = 0xFFFFFFFF;

        newOptionData.Setting = settingsUnit;
    }
}