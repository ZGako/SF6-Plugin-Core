global using IUICustomReplayElementInitialization =
    SF6_Plugin_Core.UI.InjectionShared.IUICustomElementInitialization<app.ReplayMenuItem>;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.ReplayPauseMenu.CustomElements;

namespace SF6_Plugin_Core.UI.ReplayPauseMenu.Core;

/// <summary>
///     Identifies the menu data slot inside <c>app.UIReplayPauseMenuComp._MenuData</c>.
/// </summary>
public enum ReplayPauseMenuSlot
{
    WatchReplay = 0,
    Hijack = 1
}

/// <summary>
///     Typed <see cref="AppendElementsRequest{TNode, TType, TInstantiation}" /> for injecting custom
///     <see cref="UIReplayCustomElementNode" /> entries into a slot of SF6's Replay Pause Menu via
///     <see cref="ReplayPauseMenuManager" />.
/// </summary>
/// <param name="newElements">
///     Pairs of <c>(attachmentSlot, elementNode)</c> using 1-based original-array slots (<c>0</c> = top,
///     <c>1..N</c> = after original element <c>slot - 1</c>, <c>-1</c> = bottom).
/// </param>
public class ReplayPauseMenuAppendElementsRequest(
    List<(int, UIReplayCustomElementNode)> newElements)
    : AppendElementsRequest<UIReplayCustomElementNode, app.ReplayMenuItem, IUICustomReplayElementInitialization>(
        newElements)
{
    internal System.Collections.Generic.IEnumerable<Guid> GetElementFunctionIds()
    {
        return NewElements.Select(entry => entry.Item2.GetFunctionId());
    }
}
