using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

/// <summary>
///     Abstract base initializer for custom SF6 Options UI units (<see cref="CustomOptionGroup" /> and
///     <see cref="CustomOptionValue" />), managing native <see cref="app.Option.OptionSettingUnit" /> population
///     and localized message lifetimes.
/// </summary>
public abstract class CustomOptionBase : IUICustomOptionsElementInitialization
{
    /// <summary>Localized option label entries populated into <c>OptionSettingUnit.ValueMessageList</c> (used by discrete text spinners).</summary>
    protected readonly List<CustomMessage> ValueMessageList = [];

    /// <summary>Help text shown in the bottom description panel when this option is focused.</summary>
    protected CustomMessage? DescriptionMessage;

    /// <summary>Row label displayed on the left side of the option entry.</summary>
    protected CustomMessage? TitleMessage;

    /// <summary>Globalized native <see cref="app.Option.OptionUnitBase" /> subclass instance owned by this initializer.</summary>
    protected ManagedObject? _customOptionDataMo;

    /// <summary>Globalized native <see cref="app.Option.OptionSettingUnit" /> backing <c>OptionUnitBase.Setting</c>.</summary>
    protected ManagedObject? _settingsUnitMo;

    /// <summary>Whether <c>OptionSettingUnit.IsOnlineSetting</c> should be marked true (typically reserved for online-only options).</summary>
    protected bool isOnline;

    // prevents outside packages from deriving from the base class directly
    internal CustomOptionBase()
    {
    }

    /// <summary>Discriminator telling SF6 whether this unit is a <see cref="app.Option.SettingDataType.Group" /> or <see cref="app.Option.SettingDataType.Value" />.</summary>
    protected abstract app.Option.SettingDataType SettingDataType { get; init; }

    /// <summary>Game modes in which SF6 renders or enables this option row (defaults to <see cref="app.Option.SupportModeType.All" />).</summary>
    protected app.Option.SupportModeType SupportModeType { get; init; } = app.Option.SupportModeType.All;

    /// <summary>Platforms on which this option row is active (defaults to <see cref="app.Option.SupportPlatform.All" />).</summary>
    protected app.Option.SupportPlatform SupportPlatform { get; init; } = app.Option.SupportPlatform.All;

    /// <summary>Visual widget kind rendered for this option row (button, slider, spin text, on/off spin, etc.).</summary>
    protected app.Option.UnitInputType UnitInputType { get; init; } = app.Option.UnitInputType.Button_Type0;

    /// <summary>Vanilla value type preset (overwritten by <see cref="CustomOptionValue" /> with a newly allocated custom ID).</summary>
    protected app.Option.ValueType ValueType { get; init; } = app.Option.ValueType.Invalid;

    /// <summary>Vanilla group type preset (overwritten by <see cref="CustomOptionGroup" /> with a newly allocated custom ID).</summary>
    protected app.Option.GroupType GroupType { get; init; } = app.Option.GroupType.Invalid;

    /// <summary>Native action triggered when pressing confirm on a button/submenu group row.</summary>
    protected app.Option.DecideEventType DecideEventType { get; init; } = app.Option.DecideEventType.Invalid;

    /// <summary>Optional secondary button event bound to the option row.</summary>
    protected app.Option.ExtraButtonEventType ExtraButtonEventType { get; init; } =
        app.Option.ExtraButtonEventType.Invalid;

    /// <inheritdoc />
    public abstract app.Option.OptionUnitBase InitializeCustomElement(Guid functionNameId);

    /// <inheritdoc />
    public abstract app.Option.OptionUnitBase GetElementData();

    /// <summary>
    ///     Disposes all associated title, description, and value-list <see cref="CustomMessage" /> instances and
    ///     releases the globalized <c>OptionUnitBase</c> and <c>OptionSettingUnit</c> engine objects.
    /// </summary>
    public virtual void Clear()
    {
        DescriptionMessage?.Dispose();
        TitleMessage?.Dispose();

        foreach (var message in ValueMessageList) message.Dispose();

        _customOptionDataMo?.ReleaseIfGlobalized();
        _customOptionDataMo = null;

        _settingsUnitMo?.ReleaseIfGlobalized();
        _settingsUnitMo = null;
    }

    /// <summary>
    ///     Re-registers all attached <see cref="CustomMessage" /> instances in the localization hook table so reused
    ///     nodes remain valid after an Options UI rebuild.
    /// </summary>
    protected void EnsureMessageRegistration()
    {
        TitleMessage?.EnsureRegistration();
        DescriptionMessage?.EnsureRegistration();
        foreach (var message in ValueMessageList) message.EnsureRegistration();
    }

    /// <summary>
    ///     Populates <see cref="_settingsUnitMo" /> with common display, support-mode, event, and localized message
    ///     GUID fields and attaches it to <see cref="_customOptionDataMo" />. Must be called by subclasses after
    ///     allocating both native instances.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if <see cref="_customOptionDataMo" /> or <see cref="_settingsUnitMo" /> is <see langword="null" />.
    /// </exception>
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
        settingsUnit.ValueMessageList.Clear();
        foreach (var message in ValueMessageList)
        {
            var newEngineGuid = _System.Guid.Parse(message.Id.ToString());
            settingsUnit.ValueMessageList.Add(newEngineGuid);
        }

        settingsUnit.ChangeValueSETriggerId = 0xFFFFFFFF;

        newOptionData.Setting = settingsUnit;
    }
}