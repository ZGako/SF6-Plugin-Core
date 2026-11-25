using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

public static class TrainingFunctionHooks
{
    /// <summary>
    ///     delegate type for all functions are the same signature. Return value signifies whether the menu should close after
    ///     the function is executed.
    /// </summary>
    /// <param name="baseParam">parent param calling the function (presumably)</param>
    /// <param name="viewData">
    ///     viewData of the object the function belongs to. can be used to read the value of the slider or
    ///     similar
    /// </param>
    /// \
    /// <param name="focusIndex">
    ///     value used for different functions. In the main menu, it appears to be used to indicate which row the element is
    ///     associated with.
    ///     In the SpinList submenus, it instead is used per each spinner to indicate which index value it is currently on (so
    ///     duplicate information with what is present int he viewdata basically)
    /// </param>
    public delegate FunctionResult FunctionDelegate(app.training.BaseParam baseParam,
        app.training.UIFlowTrainingMenu.Param.ViewData viewData, int focusIndex);

    // A dictionary mapping our custom FuncType integers to C# lambdas
    private static readonly ConcurrentDictionary<Guid, FunctionDelegate> CustomFunctions = [];

    private static readonly ConcurrentDictionary<Guid, FunctionDelegate> CustomOptionSelectFunctions = [];

    private static readonly ConcurrentDictionary<Guid, FunctionDelegate> CustomOptionXFunctions = [];
    private static readonly ConcurrentDictionary<Guid, FunctionDelegate> CustomOptionYFunctions = [];

    [ThreadStatic] private static Stack<(bool Handled, bool ReturnValue)>? s_functionCallStack;
    [ThreadStatic] private static Stack<(bool Handled, bool ReturnValue)>? s_optionSelectCallStack;
    [ThreadStatic] private static Stack<(bool Handled, bool ReturnValue)>? s_optionXCallStack;
    [ThreadStatic] private static Stack<(bool Handled, bool ReturnValue)>? s_optionYCallStack;

    private static void PushCallState(ref Stack<(bool Handled, bool ReturnValue)>? stack, bool handled,
        bool returnValue)
    {
        stack ??= new Stack<(bool Handled, bool ReturnValue)>();
        stack.Push((handled, returnValue));
    }

    private static bool TryPopCallState(Stack<(bool Handled, bool ReturnValue)>? stack, out bool returnValue)
    {
        if (stack != null && stack.TryPop(out var state) && state.Handled)
        {
            returnValue = state.ReturnValue;
            return true;
        }

        returnValue = false;
        return false;
    }

    // Function specific methods

    internal static void RegisterCustomFunction(Guid id, FunctionDelegate action)
    {
        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomFunctions[id] = action;
        else
            throw new InvalidOperationException(
                $"Custom function '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomFunction(Guid id)
    {
        CustomFunctions.TryRemove(id, out var _);
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc), "Function", MethodHookType.Pre)]
    public static PreHookResult OnFunctionPre(Span<ulong> args)
    {
        // FuncType argument
        var funcType = (int)args[2];
        var handled = false;
        var returnValue = false;
        var hookResult = PreHookResult.Continue;

        if (FunctionTypeRegistry.TryGetFunctionId(funcType, out var functionId) &&
            CustomFunctions.TryGetValue(functionId, out var action))
            try
            {
                var baseParam = MemoryHelpers.GetAddressAs<app.training.BaseParam>(args[3]);
                var viewData = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param.ViewData>(args[4]);

                // Invoke function
                var result = action.Invoke(baseParam, viewData, (int)args[5]);
                handled = true;
                returnValue = result.ReturnValue;
                hookResult = result.HookResult;
            }
            catch (Exception ex)
            {
                API.LogError($"Exception in OnFunctionPre: {ex.Message}");
            }

        PushCallState(ref s_functionCallStack, handled, returnValue);
        return hookResult;
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc), "Function", MethodHookType.Post)]
    public static void OnFunctionPost(ref ulong retval)
    {
        if (TryPopCallState(s_functionCallStack, out var returnValue))
            // output is Boolean = true
            retval = returnValue ? 1UL : 0UL;
    }

    internal static void RegisterCustomOptionSelectFunction(Guid id, FunctionDelegate action)
    {
        // Generate a unique FuncType integer for this custom function

        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomOptionSelectFunctions[id] = action;
        else
            throw new InvalidOperationException(
                $"Custom option select function '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomOptionSelectFunction(Guid id)
    {
        CustomOptionSelectFunctions.TryRemove(id, out var _);
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc),
        "OptionSelectFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)",
        MethodHookType.Pre)]
    public static PreHookResult OnOptionSelectFunctionPre(Span<ulong> args)
    {
        // FuncType argument
        var funcType = (int)args[2];
        var handled = false;
        var returnValue = false;
        var hookResult = PreHookResult.Continue;

        if (FunctionTypeRegistry.TryGetFunctionId(funcType, out var functionId) &&
            CustomOptionSelectFunctions.TryGetValue(functionId!, out var action))
            try
            {
                var rowIndex = (int)args[5];

                var baseParam = MemoryHelpers.GetAddressAs<app.training.BaseParam>(args[3]);
                var viewData = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param.ViewData>(args[4]);

                // Invoke function
                var result = action.Invoke(baseParam, viewData, rowIndex);
                handled = true;
                returnValue = result.ReturnValue;
                hookResult = result.HookResult;
            }
            catch (Exception ex)
            {
                API.LogError($"Exception in OnOptionSelectFunctionPre: {ex.Message}");
            }

        PushCallState(ref s_optionSelectCallStack, handled, returnValue);
        return hookResult;
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc),
        "OptionSelectFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)",
        MethodHookType.Post)]
    public static void OnOptionSelectFunctionPost(ref ulong retval)
    {
        if (TryPopCallState(s_optionSelectCallStack, out var returnValue))
            // output is Boolean, determines whether or not the menu should close.
            retval = returnValue ? 1UL : 0UL;
    }


    internal static void RegisterCustomOptionXFunction(Guid id, FunctionDelegate action)
    {
        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomOptionXFunctions[id] = action;
        else
            throw new InvalidOperationException(
                $"Custom option X function '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomOptionXFunction(Guid id)
    {
        CustomOptionXFunctions.TryRemove(id, out var _);
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc),
        "OptionXFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)",
        MethodHookType.Pre)]
    public static PreHookResult OnOptionXFunctionPre(Span<ulong> args)
    {
        var funcType = (int)args[2];
        var handled = false;
        var returnValue = false;
        var hookResult = PreHookResult.Continue;

        if (FunctionTypeRegistry.TryGetFunctionId(funcType, out var functionId) &&
            CustomOptionXFunctions.TryGetValue(functionId, out var action))
            try
            {
                var rowIndex = (int)args[5];
                var baseParam = MemoryHelpers.GetAddressAs<app.training.BaseParam>(args[3]);
                var viewData = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param.ViewData>(args[4]);
                var result = action.Invoke(baseParam, viewData, rowIndex);
                handled = true;
                returnValue = result.ReturnValue;
                hookResult = result.HookResult;
            }
            catch (Exception ex)
            {
                API.LogError($"Exception in OnOptionXFunctionPre: {ex.Message}");
            }

        PushCallState(ref s_optionXCallStack, handled, returnValue);
        return hookResult;
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc),
        "OptionXFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)",
        MethodHookType.Post)]
    public static void OnOptionXFunctionPost(ref ulong retval)
    {
        if (TryPopCallState(s_optionXCallStack, out var returnValue))
            retval = returnValue ? 1UL : 0UL;
    }

    internal static void RegisterCustomOptionYFunction(Guid id, FunctionDelegate action)
    {
        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomOptionYFunctions[id] = action;
        else
            throw new InvalidOperationException(
                $"Custom option Y function '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomOptionYFunction(Guid id)
    {
        CustomOptionYFunctions.TryRemove(id, out var _);
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc),
        "OptionYFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)",
        MethodHookType.Pre)]
    public static PreHookResult OnOptionYFunctionPre(Span<ulong> args)
    {
        var funcType = (int)args[2];
        var handled = false;
        var returnValue = false;
        var hookResult = PreHookResult.Continue;

        if (FunctionTypeRegistry.TryGetFunctionId(funcType, out var functionId) &&
            CustomOptionYFunctions.TryGetValue(functionId, out var action))
            try
            {
                var rowIndex = (int)args[5];
                var baseParam = MemoryHelpers.GetAddressAs<app.training.BaseParam>(args[3]);
                var viewData = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param.ViewData>(args[4]);
                var result = action.Invoke(baseParam, viewData, rowIndex);
                handled = true;
                returnValue = result.ReturnValue;
                hookResult = result.HookResult;
            }
            catch (Exception ex)
            {
                API.LogError($"Exception in OnOptionYFunctionPre: {ex.Message}");
            }

        PushCallState(ref s_optionYCallStack, handled, returnValue);
        return hookResult;
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc),
        "OptionYFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)",
        MethodHookType.Post)]
    public static void OnOptionYFunctionPost(ref ulong retval)
    {
        if (TryPopCallState(s_optionYCallStack, out var returnValue))
            retval = returnValue ? 1UL : 0UL;
    }

    internal static void Clear()
    {
        CustomFunctions.Clear();
        CustomOptionSelectFunctions.Clear();
        CustomOptionXFunctions.Clear();
        CustomOptionYFunctions.Clear();
    }

    public readonly struct FunctionResult
    {
        public readonly bool ReturnValue;
        public readonly PreHookResult HookResult;

        public FunctionResult(bool returnValue, PreHookResult hookResult = PreHookResult.Skip)
        {
            ReturnValue = returnValue;
            HookResult = hookResult;
        }

        // Allows `return true;` or `return false;` to default to PreHookResult.Skip
        public static implicit operator FunctionResult(bool returnValue)
        {
            return new FunctionResult(returnValue);
        }

        // Optional: allows `return (false, PreHookResult.Continue);` for clean tuple syntax
        public static implicit operator FunctionResult((bool returnValue, PreHookResult hookResult) tuple)
        {
            return new FunctionResult(tuple.returnValue, tuple.hookResult);
        }
    }
}