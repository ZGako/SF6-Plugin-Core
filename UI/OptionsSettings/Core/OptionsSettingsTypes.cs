global using IUICustomOptionsElementInitialization =
    SF6_Plugin_Core.UI.InjectionShared.IUICustomElementInitialization<app.Option.OptionUnitBase>;
global using IUICustomOptionsGroupElementInitialization =
    SF6_Plugin_Core.UI.InjectionShared.IUICustomElementInitialization<app.Option.OptionGroupUnit>;
global using IUICustomOptionsValueElementInitialization =
    SF6_Plugin_Core.UI.InjectionShared.IUICustomElementInitialization<app.Option.OptionValueUnit>;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.OptionsSettings.CustomOptions;

namespace SF6_Plugin_Core.UI.OptionsSettings.Core;

/// <summary>
///     Typed <see cref="AppendElementsRequest{TNode, TType, TInstantiation}" /> for injecting custom
///     <see cref="UICustomOptionRequestNode" /> groups and value rows into SF6's Options menu tabs or nested group
///     lists via <see cref="OptionsSettingsManager.RegisterModification(List{int}, IUIModificationRequest)" />.
/// </summary>
/// <param name="newElements">
///     Pairs of <c>(attachmentSlot, optionNode)</c> using 1-based original-array slots (<c>0</c> = top,
///     <c>1..N</c> = after original element <c>slot - 1</c>, <c>-1</c> = bottom).
/// </param>
public class OptionsAppendElementsRequest(
    List<(int, UICustomOptionRequestNode)> newElements)
    : AppendElementsRequest<UICustomOptionRequestNode, app.Option.OptionUnitBase,
        IUICustomOptionsElementInitialization>(newElements);