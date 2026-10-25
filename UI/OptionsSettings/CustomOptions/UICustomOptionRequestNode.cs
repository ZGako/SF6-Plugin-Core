using app;
using SF6_Plugin_Core.UI.InjectionShared;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

/// <summary>
///     Abstract class meant to be used as the base of your collections. To initialize use the inherited classes
/// </summary>
/// <param name="functionId"></param>
/// <param name="customInitializer"></param>
public abstract class UICustomOptionRequestNode(
    Guid functionId,
    IUICustomOptionsElementInitialization customInitializer) :
    UICustomRequestNode<Option.OptionUnitBase, IUICustomOptionsElementInitialization>(functionId,
        customInitializer)
{
}

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