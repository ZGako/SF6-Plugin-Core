global using IUICustomOptionsElementInitialization =
    SF6_Plugin_Core.UI.InjectionShared.IUICustomElementInitialization<app.Option.OptionUnitBase>;
global using IUICustomOptionsGroupElementInitialization =
    SF6_Plugin_Core.UI.InjectionShared.IUICustomElementInitialization<app.Option.OptionGroupUnit>;
global using IUICustomOptionsValueElementInitialization =
    SF6_Plugin_Core.UI.InjectionShared.IUICustomElementInitialization<app.Option.OptionValueUnit>;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

namespace SF6_Plugin_Core.UI.OptionsSettings.Core;

public class OptionsAppendElementsRequest(
    List<(int, UICustomOptionRequestNode)> newElements)
    : AppendElementsRequest<UICustomOptionRequestNode, app.Option.OptionUnitBase,
        IUICustomOptionsElementInitialization>(newElements);