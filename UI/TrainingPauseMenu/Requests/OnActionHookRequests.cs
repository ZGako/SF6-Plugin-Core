using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

/// <summary>
///     Attaches a primary action handler to <c>app.training.TrainingMenuFunc.Function</c> for a vanilla or custom
///     Training Pause Menu element (button presses, spinner choice commits, slider changes, or submenu spinner
///     commits).
/// </summary>
/// <param name="functionDelegate">Handler invoked when the element's <c>FuncType</c> is executed.</param>
public class FunctionHookRequest(TrainingFunctionHooks.FunctionDelegate functionDelegate)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly TrainingFunctionHooks.FunctionDelegate _functionDelegate = functionDelegate;

    private Guid _functionId;

    /// <inheritdoc />
    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        TrainingFunctionHooks.RegisterCustomFunction(functionId, _functionDelegate);
    }

    /// <inheritdoc />
    public void ClearHook()
    {
        TrainingFunctionHooks.UnregisterCustomFunction(_functionId);
    }

    /// <inheritdoc />
    public override FunctionHookRequest Clone()
    {
        return new FunctionHookRequest(_functionDelegate);
    }
}

/// <summary>
///     Attaches a secondary-action handler to <c>app.training.TrainingMenuFunc.OptionSelectFunction</c> (typically
///     resetting a focused row to default or triggering a row-level special action) for a vanilla or custom element.
/// </summary>
/// <param name="functionDelegate">Handler invoked when the Option Select input triggers on the element.</param>
public class OptionSelectHookRequest(TrainingFunctionHooks.FunctionDelegate functionDelegate)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly TrainingFunctionHooks.FunctionDelegate _functionDelegate = functionDelegate;

    private Guid _functionId;

    /// <inheritdoc />
    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        TrainingFunctionHooks.RegisterCustomOptionSelectFunction(functionId, _functionDelegate);
    }

    /// <inheritdoc />
    public void ClearHook()
    {
        TrainingFunctionHooks.UnregisterCustomOptionSelectFunction(_functionId);
    }

    /// <inheritdoc />
    public override OptionSelectHookRequest Clone()
    {
        return new OptionSelectHookRequest(_functionDelegate);
    }
}

/// <summary>
///     Attaches a handler to <c>app.training.TrainingMenuFunc.OptionXFunction</c> (used by prompts bound to
///     <see cref="app.InputAssign.Digital.Id.UITrainingOptionX" />, such as randomizer configuration submenus).
/// </summary>
/// <param name="functionDelegate">Handler invoked when the Option X input is pressed on the focused element.</param>
public class OptionXHookRequest(TrainingFunctionHooks.FunctionDelegate functionDelegate)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly TrainingFunctionHooks.FunctionDelegate _functionDelegate = functionDelegate;

    private Guid _functionId;

    /// <inheritdoc />
    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        TrainingFunctionHooks.RegisterCustomOptionXFunction(functionId, _functionDelegate);
    }

    /// <inheritdoc />
    public void ClearHook()
    {
        TrainingFunctionHooks.UnregisterCustomOptionXFunction(_functionId);
    }

    /// <inheritdoc />
    public override OptionXHookRequest Clone()
    {
        return new OptionXHookRequest(_functionDelegate);
    }
}

/// <summary>
///     Attaches a handler to <c>app.training.TrainingMenuFunc.OptionYFunction</c> (used by prompts bound to
///     <see cref="app.InputAssign.Digital.Id.UITrainingOptionY" />, such as opening custom tuning submenus).
/// </summary>
/// <param name="functionDelegate">Handler invoked when the Option Y input is pressed on the focused element.</param>
public class OptionYHookRequest(TrainingFunctionHooks.FunctionDelegate functionDelegate)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly TrainingFunctionHooks.FunctionDelegate _functionDelegate = functionDelegate;

    private Guid _functionId;

    /// <inheritdoc />
    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        TrainingFunctionHooks.RegisterCustomOptionYFunction(functionId, _functionDelegate);
    }

    /// <inheritdoc />
    public void ClearHook()
    {
        TrainingFunctionHooks.UnregisterCustomOptionYFunction(_functionId);
    }

    /// <inheritdoc />
    public override OptionYHookRequest Clone()
    {
        return new OptionYHookRequest(_functionDelegate);
    }
}