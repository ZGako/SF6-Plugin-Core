using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.ReplayPauseMenu.Core;

namespace SF6_Plugin_Core.UI.ReplayPauseMenu.CustomElements;

/// <summary>
///     Flat request node representing an injected <see cref="app.ReplayMenuItem" /> entry for SF6's Replay Pause Menu.
/// </summary>
public class UIReplayCustomElementNode(Guid functionId, IUICustomReplayElementInitialization customInitializer)
    : UICustomRequestNode<app.ReplayMenuItem, IUICustomReplayElementInitialization>(functionId, customInitializer)
{
    internal Guid GetFunctionId() => FunctionId;

    internal override app.ReplayMenuItem Build()
    {
        var item = CustomInitializer.InitializeCustomElement(FunctionId);

        if (CustomInitializer is CustomReplayItemBase { GetInitialSpinIndex: { } getInitialIndex })
            ReplayPauseMenuManager.RegisterSpinnerInitializer(FunctionId, getInitialIndex);

        foreach (var hook in HookInstructions) hook.AddHook(FunctionId);
        return item;
    }
}

