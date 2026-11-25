global using IUICustomTrainingElementInitialization =
    SF6_Plugin_Core.UI.InjectionShared.IUICustomElementInitialization<app.training.TrainingMenuData>;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;


namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

/// <summary>
///     Typed <see cref="AppendElementsRequest{TNode, TType, TInstantiation}" /> for injecting custom
///     <see cref="UICustomElementRequestNode" /> rows, tabs, spinner options, or submenu items into SF6's Training
///     Pause Menu via <see cref="PauseMenuManager.RegisterModification" />.
/// </summary>
/// <param name="newElements">
///     Pairs of <c>(attachmentSlot, elementNode)</c> using 1-based original-array slots (<c>0</c> = top,
///     <c>1..N</c> = after original element <c>slot - 1</c>, <c>-1</c> = bottom).
/// </param>
public class
    PauseMenuAppendElementsRequest(
        List<(int, UICustomElementRequestNode)>
            newElements) : AppendElementsRequest<UICustomElementRequestNode, app.training.TrainingMenuData,
    IUICustomTrainingElementInitialization>(newElements);

/// <summary>
///     Discriminated union representing either a built-in vanilla <see cref="app.training.TrainingFuncType" /> or a
///     mod-defined custom <see cref="Guid" /> function ID (used by <c>SpinBoxHook</c> return values).
/// </summary>
public readonly struct TrainingFuncOrGuid
{
    internal readonly bool IsGuid;
    internal readonly app.training.TrainingFuncType IntValue;
    internal readonly Guid GuidValue;

    private TrainingFuncOrGuid(app.training.TrainingFuncType intValue)
    {
        IsGuid = false;
        IntValue = intValue;
        GuidValue = Guid.Empty;
    }

    private TrainingFuncOrGuid(Guid guidValue)
    {
        IsGuid = true;
        IntValue = 0;
        GuidValue = guidValue;
    }

    /// <summary>
    ///     Implicitly wraps a vanilla <see cref="app.training.TrainingFuncType" /> so <c>SpinBoxHook.SpinBoxInitDelegate</c>
    ///     implementations can select stock spinner options directly.
    /// </summary>
    /// <param name="intValue">A vanilla game <see cref="app.training.TrainingFuncType" /> enum value.</param>
    public static implicit operator TrainingFuncOrGuid(app.training.TrainingFuncType intValue)
    {
        return new TrainingFuncOrGuid(intValue);
    }

    /// <summary>
    ///     Implicitly wraps a mod-defined custom function <see cref="Guid" /> so <c>SpinBoxHook.SpinBoxInitDelegate</c>
    ///     implementations can select injected custom spinner items.
    /// </summary>
    /// <param name="guidValue">The <see cref="Guid" /> passed to the custom spinner item's <see cref="UICustomElementRequestNode" />.</param>
    public static implicit operator TrainingFuncOrGuid(Guid guidValue)
    {
        return new TrainingFuncOrGuid(guidValue);
    }
}