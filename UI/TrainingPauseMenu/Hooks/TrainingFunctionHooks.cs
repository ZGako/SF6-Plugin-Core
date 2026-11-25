using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

/// <summary>
///     Intercepts <see cref="app.training.TrainingMenuFunc" /> action dispatchers (<c>Function</c>,
///     <c>OptionSelectFunction</c>, <c>OptionXFunction</c>, and <c>OptionYFunction</c>) to route custom and hooked
///     vanilla <see cref="app.training.TrainingFuncType" /> interactions to mod delegates.
/// </summary>
public static class TrainingFunctionHooks
{
    /// <summary>
    ///     Handler invoked when a hooked or custom Training Pause Menu item executes its primary action or option
    ///     shortcut (<c>Select</c>, <c>Option X</c>, or <c>Option Y</c>).
    /// </summary>
    /// <param name="baseParam">Parent menu flow parameter (typically <see cref="app.training.UIFlowTrainingMenu.Param" /> or <c>SpinList.Param</c>).</param>
    /// <param name="viewData">Live <see cref="app.training.UIFlowTrainingMenu.Param.ViewData" /> for the item (used to inspect <c>Index</c>, <c>Data</c>, or slider values).</param>
    /// <param name="focusIndex">
    ///     Focused row index in the primary/secondary menu, or the current domain value (<c>Index + LimitValue.x</c>)
    ///     when invoked by <see cref="SubMenuSpinnerHook" /> inside a <c>SPINLIST</c> submenu.
    /// </param>
    /// <returns>
    ///     A <see cref="FunctionResult" /> where <c>ReturnValue</c> tells SF6 whether the pause menu should close and
    ///     <c>HookResult</c> controls whether the vanilla <c>TrainingMenuFunc</c> handler is skipped or also runs.
    /// </returns>
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

    /// <summary>
    ///     REFramework pre-hook on <c>app.training.TrainingMenuFunc.Function</c> (not intended for direct calls by mods).
    ///     Invokes the registered <see cref="FunctionDelegate" /> for <c>args[2]</c> and records its return override
    ///     for <see cref="OnFunctionPost" />.
    /// </summary>
    /// <param name="args">
    ///     Raw VM argument span: <c>args[2]</c> is <c>FuncType</c>, <c>args[3]</c> is <c>BaseParam</c>, <c>args[4]</c>
    ///     is <c>ViewData</c>, and <c>args[5]</c> is integer index/value.
    /// </param>
    /// <returns>Delegate's <see cref="FunctionResult.HookResult" />, or <see cref="PreHookResult.Continue" /> when unhandled.</returns>
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

    /// <summary>
    ///     REFramework post-hook on <c>app.training.TrainingMenuFunc.Function</c> (not intended for direct calls by
    ///     mods). Writes the custom boolean close-menu return value into <paramref name="retval" /> when handled in pre.
    /// </summary>
    /// <param name="retval">Raw boolean return register indicating whether SF6 should close the pause menu.</param>
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

    /// <summary>
    ///     REFramework pre-hook on <c>app.training.TrainingMenuFunc.OptionSelectFunction</c> (not intended for direct
    ///     calls by mods). Dispatches <c>OptionSelectHookRequest</c> delegates registered for <c>args[2]</c>.
    /// </summary>
    /// <param name="args">
    ///     Raw VM argument span: <c>args[2]</c> is <c>FuncType</c>, <c>args[3]</c> is <c>BaseParam</c>, <c>args[4]</c>
    ///     is <c>ViewData</c>, and <c>args[5]</c> is row index.
    /// </param>
    /// <returns>Delegate's <see cref="FunctionResult.HookResult" />, or <see cref="PreHookResult.Continue" /> when unhandled.</returns>
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

    /// <summary>
    ///     REFramework post-hook on <c>app.training.TrainingMenuFunc.OptionSelectFunction</c> (not intended for direct
    ///     calls by mods). Applies the custom boolean close-menu return value to <paramref name="retval" /> when handled.
    /// </summary>
    /// <param name="retval">Raw boolean return register indicating whether SF6 should close the pause menu.</param>
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

    /// <summary>
    ///     REFramework pre-hook on <c>app.training.TrainingMenuFunc.OptionXFunction</c> (not intended for direct calls
    ///     by mods). Dispatches <c>OptionXHookRequest</c> delegates registered for <c>args[2]</c>.
    /// </summary>
    /// <param name="args">
    ///     Raw VM argument span: <c>args[2]</c> is <c>FuncType</c>, <c>args[3]</c> is <c>BaseParam</c>, <c>args[4]</c>
    ///     is <c>ViewData</c>, and <c>args[5]</c> is row index.
    /// </param>
    /// <returns>Delegate's <see cref="FunctionResult.HookResult" />, or <see cref="PreHookResult.Continue" /> when unhandled.</returns>
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

    /// <summary>
    ///     REFramework post-hook on <c>app.training.TrainingMenuFunc.OptionXFunction</c> (not intended for direct calls
    ///     by mods). Applies the custom boolean close-menu return value to <paramref name="retval" /> when handled.
    /// </summary>
    /// <param name="retval">Raw boolean return register indicating whether SF6 should close the pause menu.</param>
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

    /// <summary>
    ///     REFramework pre-hook on <c>app.training.TrainingMenuFunc.OptionYFunction</c> (not intended for direct calls
    ///     by mods). Dispatches <c>OptionYHookRequest</c> delegates registered for <c>args[2]</c>.
    /// </summary>
    /// <param name="args">
    ///     Raw VM argument span: <c>args[2]</c> is <c>FuncType</c>, <c>args[3]</c> is <c>BaseParam</c>, <c>args[4]</c>
    ///     is <c>ViewData</c>, and <c>args[5]</c> is row index.
    /// </param>
    /// <returns>Delegate's <see cref="FunctionResult.HookResult" />, or <see cref="PreHookResult.Continue" /> when unhandled.</returns>
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

    /// <summary>
    ///     REFramework post-hook on <c>app.training.TrainingMenuFunc.OptionYFunction</c> (not intended for direct calls
    ///     by mods). Applies the custom boolean close-menu return value to <paramref name="retval" /> when handled.
    /// </summary>
    /// <param name="retval">Raw boolean return register indicating whether SF6 should close the pause menu.</param>
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

    /// <summary>
    ///     Return value returned by <see cref="FunctionDelegate" /> combining the menu close boolean and REFramework
    ///     <see cref="PreHookResult" /> behavior. Returning a plain <see langword="bool" /> implicitly selects
    ///     <see cref="PreHookResult.Skip" /> so vanilla handlers are suppressed unless explicitly continued.
    /// </summary>
    public readonly struct FunctionResult
    {
        /// <summary>Boolean value forwarded back to SF6's menu caller (typically <see langword="true" /> to close the menu).</summary>
        public readonly bool ReturnValue;

        /// <summary>Controls whether REFramework skips or also executes SF6's original <c>TrainingMenuFunc</c> handler.</summary>
        public readonly PreHookResult HookResult;

        /// <summary>
        ///     Constructs a <see cref="FunctionResult" /> with explicit close-menu return and hook continuation control.
        /// </summary>
        /// <param name="returnValue">Boolean returned to SF6 (<see langword="true" /> to close the menu).</param>
        /// <param name="hookResult">Whether to skip (default) or continue into the original game function.</param>
        public FunctionResult(bool returnValue, PreHookResult hookResult = PreHookResult.Skip)
        {
            ReturnValue = returnValue;
            HookResult = hookResult;
        }

        // Allows `return true;` or `return false;` to default to PreHookResult.Skip
        /// <summary>
        ///     Implicitly converts a plain <see langword="bool" /> return value to a <see cref="FunctionResult" /> with
        ///     <see cref="PreHookResult.Skip" />.
        /// </summary>
        /// <param name="returnValue">Boolean returned to SF6 (<see langword="true" /> to close the menu).</param>
        public static implicit operator FunctionResult(bool returnValue)
        {
            return new FunctionResult(returnValue);
        }

        // Optional: allows `return (false, PreHookResult.Continue);` for clean tuple syntax
        /// <summary>
        ///     Implicitly converts a <c>(returnValue, hookResult)</c> tuple into a <see cref="FunctionResult" /> (for
        ///     example <c>return (false, PreHookResult.Continue);</c> to observe without suppressing vanilla behavior).
        /// </summary>
        /// <param name="tuple">Pair specifying the SF6 boolean return value and REFramework <see cref="PreHookResult" />.</param>
        public static implicit operator FunctionResult((bool returnValue, PreHookResult hookResult) tuple)
        {
            return new FunctionResult(tuple.returnValue, tuple.hookResult);
        }
    }
}