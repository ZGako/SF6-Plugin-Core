using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

public class FunctionHookRequest(TrainingFunctionHooks.FunctionDelegate functionDelegate)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly TrainingFunctionHooks.FunctionDelegate _functionDelegate = functionDelegate;

    private Guid _functionId;

    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        TrainingFunctionHooks.RegisterCustomFunction(functionId, _functionDelegate);
    }

    public void ClearHook()
    {
        TrainingFunctionHooks.UnregisterCustomFunction(_functionId);
    }

    public override FunctionHookRequest Clone()
    {
        return new FunctionHookRequest(_functionDelegate);
    }
}

public class OptionSelectHookRequest(TrainingFunctionHooks.FunctionDelegate functionDelegate)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly TrainingFunctionHooks.FunctionDelegate _functionDelegate = functionDelegate;

    private Guid _functionId;

    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        TrainingFunctionHooks.RegisterCustomOptionSelectFunction(functionId, _functionDelegate);
    }

    public void ClearHook()
    {
        TrainingFunctionHooks.UnregisterCustomOptionSelectFunction(_functionId);
    }

    public override OptionSelectHookRequest Clone()
    {
        return new OptionSelectHookRequest(_functionDelegate);
    }
}

public class OptionXHookRequest(TrainingFunctionHooks.FunctionDelegate functionDelegate)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly TrainingFunctionHooks.FunctionDelegate _functionDelegate = functionDelegate;

    private Guid _functionId;

    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        TrainingFunctionHooks.RegisterCustomOptionXFunction(functionId, _functionDelegate);
    }

    public void ClearHook()
    {
        TrainingFunctionHooks.UnregisterCustomOptionXFunction(_functionId);
    }

    public override OptionXHookRequest Clone()
    {
        return new OptionXHookRequest(_functionDelegate);
    }
}

public class OptionYHookRequest(TrainingFunctionHooks.FunctionDelegate functionDelegate)
    : ExclusiveModificationRequest, IUIHookRequest
{
    private readonly TrainingFunctionHooks.FunctionDelegate _functionDelegate = functionDelegate;

    private Guid _functionId;

    public void AddHook(Guid functionId)
    {
        _functionId = functionId;
        TrainingFunctionHooks.RegisterCustomOptionYFunction(functionId, _functionDelegate);
    }

    public void ClearHook()
    {
        TrainingFunctionHooks.UnregisterCustomOptionYFunction(_functionId);
    }

    public override OptionYHookRequest Clone()
    {
        return new OptionYHookRequest(_functionDelegate);
    }
}