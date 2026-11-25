using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

namespace SF6_Plugin_Core.UI.OptionsSettings.Requests;

// TODO: fix this eventually
internal class AppendNewTabRequest(
    List<UICustomOptionRequestNode> tabElements) : ExclusiveModificationRequest
{
    private List<UICustomOptionRequestNode> TabElements { get; } = tabElements;

    public List<app.Option.OptionUnitBase> BuildTabElements()
    {
        return TabElements.Select(node => node.Build()).ToList();
    }

    public void ClearHooks()
    {
        foreach (var elementNode in TabElements) elementNode.ClearHooks();
    }

    public void ReleaseResources()
    {
        foreach (var elementNode in TabElements) elementNode.ClearNativeResources();
    }

    public void Reset()
    {
        ClearHooks();
        ReleaseResources();
    }

    public override AppendNewTabRequest Clone()
    {
        return new AppendNewTabRequest([.. TabElements]);
    }
}