using app;
using SF6_Plugin_Core.UI.InjectionShared;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

/// <summary>
///     Abstract request node for custom Options menu entries. Instantiate <see cref="UICustomOptionsGroupNode" />
///     for submenu containers or <see cref="UICustomOptionsValueNode" /> for interactive settings rows.
/// </summary>
/// <param name="functionId">
///     Stable <see cref="Guid" /> mapped to a custom integer <see cref="Option.GroupType" /> or
///     <see cref="Option.ValueType" /> during rebuilds.
/// </param>
/// <param name="customInitializer">Element initializer backing this Options node.</param>
public abstract class UICustomOptionRequestNode(
    Guid functionId,
    IUICustomOptionsElementInitialization customInitializer) :
    UICustomRequestNode<Option.OptionUnitBase, IUICustomOptionsElementInitialization>(functionId,
        customInitializer)
{
}

/// <summary>
///     Request node representing an <see cref="Option.OptionGroupUnit" /> submenu or button entry, recursively
///     populating <c>ChildUnits</c> and binding attached hooks on rebuild.
/// </summary>
/// <param name="functionId">Stable <see cref="Guid" /> mapped to the group's runtime <see cref="Option.GroupType" />.</param>
/// <param name="customInitializer">Group initializer (such as <see cref="GroupRowButtonInitializer" />).</param>
public class UICustomOptionsGroupNode(
    Guid functionId,
    IUICustomOptionsGroupElementInitialization customInitializer) :
    UICustomOptionRequestNode(functionId, customInitializer)
{
    internal override Option.OptionGroupUnit Build()
    {
        var customGroupData = customInitializer.InitializeCustomElement(FunctionId);

        customGroupData.ChildUnits.Clear();

        // now we traverse the child nodes recursively and build their data
        // on the way up we add the hooks and create the childData arrays
        if (ChildNodes.Count > 0)
            foreach (var childData in ChildNodes.Select(t => t.Build()))
                customGroupData.ChildUnits.Add(childData);

        // add the hooks to the custom element
        foreach (var hook in HookInstructions) hook.AddHook(FunctionId);

        return customGroupData;
    }
}

/// <summary>
///     Request node representing an <see cref="Option.OptionValueUnit" /> leaf setting (sliders, spinners, toggles,
///     or static labels) without nested child units.
/// </summary>
/// <param name="functionId">Stable <see cref="Guid" /> mapped to the value row's runtime <see cref="Option.ValueType" />.</param>
/// <param name="customInitializer">Value initializer (such as <see cref="SliderOptionInitializer" /> or <see cref="SpinBoxOptionInitializer" />).</param>
public class UICustomOptionsValueNode(
    Guid functionId,
    IUICustomOptionsValueElementInitialization customInitializer) :
    UICustomOptionRequestNode(functionId, customInitializer)
{
    // remove this for now just to make it non-settable as the SubValues aren't used
    private new ManagedObject? ChildDataArrayMo = null;

    internal override Option.OptionValueUnit Build()
    {
        var customValueData = customInitializer.InitializeCustomElement(FunctionId);

        // I can't find a single instance ingame of the SubValues list being inhabited, so gonna ignore it for now I suppose
        // customValueData.ValueData.SubValue.Add();

        // add the hooks to the custom element
        foreach (var hook in HookInstructions) hook.AddHook(FunctionId);

        return customValueData;
    }
}