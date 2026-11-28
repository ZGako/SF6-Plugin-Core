using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.ReplayPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.ReplayPauseMenu.Requests;

/// <summary>
///     Attaches a decide/change handler to <c>app.esports.ReplayUICtrl.OnDecideItem</c> for a custom or hooked
///     <see cref="app.ReplayMenuItem" />.
/// </summary>
public class ReplayDecideHookRequest(ReplayDecideHooks.ReplayDecideDelegate decideDelegate)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly ReplayDecideHooks.ReplayDecideDelegate _decideDelegate = decideDelegate;
    private Guid _functionId;

    /// <summary>
    ///     Convenience constructor that automatically returns <see cref="PreHookResult.Skip" /> after executing
    ///     <paramref name="action" />.
    /// </summary>
    public ReplayDecideHookRequest(Action<app.esports.ReplayUICtrl, int, bool> action)
        : this((ctrl, scrollIndex, isBattle) =>
        {
            action(ctrl, scrollIndex, isBattle);
            return PreHookResult.Skip;
        })
    {
    }

    /// <inheritdoc />
    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        ReplayDecideHooks.RegisterDecideHandler(functionId, _decideDelegate);
    }

    /// <inheritdoc />
    public void ClearHook()
    {
        if (_functionId != Guid.Empty)
            ReplayDecideHooks.UnregisterDecideHandler(_functionId);
    }

    /// <inheritdoc />
    public override ReplayDecideHookRequest Clone()
    {
        return new ReplayDecideHookRequest(_decideDelegate);
    }
}

