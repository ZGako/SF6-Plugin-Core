using SF6_Plugin_Core.UI.OptionsSettings.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

namespace SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

public class UICustomOptionRequestNode(Guid functionId, IUICustomOptionsElementInitialization customInitializer) :
    UICustomRequestNode<app.Option.OptionUnitBase, IUICustomOptionsElementInitialization>(functionId,
        customInitializer)
{
    internal override app.Option.OptionUnitBase Build()
    {
        throw new NotImplementedException();
    }
}