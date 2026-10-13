using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

//FIXME: refactor
public class FunctionHookRequest(TrainingFunctionHooks.FunctionDelegate functionDelegate) : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly TrainingFunctionHooks.FunctionDelegate _functionDelegate = functionDelegate;

    private string? _functionName;

    public void AddDispatcher(string functionName)
    {
        _functionName = functionName;
        TrainingFunctionHooks.RegisterCustomFunction(functionName, _functionDelegate);
    }

    public void ClearDispatcher()
    {
        if (_functionName is null)
        {
            API.LogError("Cannot clear dispatcher because it was never added.");
            throw new InvalidOperationException("Cannot clear dispatcher because it was never added.");
        }
        TrainingFunctionHooks.UnregisterCustomFunction(_functionName);
    }

    public override FunctionHookRequest Clone()
    {
        return new FunctionHookRequest(_functionDelegate);
    }
}

public class OptionSelectHookRequest(TrainingFunctionHooks.FunctionDelegate functionDelegate) : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly TrainingFunctionHooks.FunctionDelegate _functionDelegate = functionDelegate;

    private string? _functionName;

    public void AddDispatcher(string functionName)
    {
        _functionName = functionName;
        TrainingFunctionHooks.RegisterCustomOptionSelectFunction(functionName, _functionDelegate);
    }

    public void ClearDispatcher()
    {
        if (_functionName is null)
        {
            API.LogError("Cannot clear dispatcher because it was never added.");
            throw new InvalidOperationException("Cannot clear dispatcher because it was never added.");
        }
        TrainingFunctionHooks.UnregisterCustomOptionSelectFunction(_functionName);
    }

    public override OptionSelectHookRequest Clone()
    {
        return new OptionSelectHookRequest(_functionDelegate);
    }
}

//TODO: add the OptionX and OptionY stuff
//TODO: maybe one can simplify the code instead of copy pasting? But whatever