using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

public static class TrainingFunctionHooks
{
    // delegate type for all functions are the same signature. Return value signifies whether or not the menu should close after the function is executed.
    public delegate bool FunctionDelegate(app.training.BaseParam baseParam,
        app.training.UIFlowTrainingMenu.Param.ViewData viewData, int rowIndex);

    // A dictionary mapping our custom FuncType integers to C# lambdas
    private static readonly ConcurrentDictionary<Guid, FunctionDelegate> CustomFunctions = [];

    private static readonly ConcurrentDictionary<Guid, FunctionDelegate> CustomOptionSelectFunctions = [];

    private static readonly ConcurrentDictionary<Guid, FunctionDelegate> CustomOptionXFunctions = [];
    private static readonly ConcurrentDictionary<Guid, FunctionDelegate> CustomOptionYFunctions = [];

    [ThreadStatic] private static bool s_handledCustomFunction;

    [ThreadStatic] private static bool s_returnValueForCustomFunction;


    // OptionSelectFunction specific methods


    // ThreadStatic ensures thread-safety if the game calls this concurrently
    [ThreadStatic] private static bool s_handledCustomOptionSelectFunction;

    [ThreadStatic] private static bool s_returnValueForCustomOptionSelectFunction;


    [ThreadStatic] private static bool s_handledCustomOptionXFunction;

    [ThreadStatic] private static bool s_returnValueForCustomOptionXFunction;

    [ThreadStatic] private static bool s_handledCustomOptionYFunction;

    [ThreadStatic] private static bool s_returnValueForCustomOptionYFunction;

    // Function specific methods

    internal static void RegisterCustomFunction(Guid id, FunctionDelegate action)
    {
        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomFunctions.TryAdd(id, action);
        else
            throw new InvalidOperationException(
                $"Custom function '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomFunction(Guid id)
    {
        CustomFunctions.Remove(id, out var _);
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc), "Function", MethodHookType.Pre)]
    public static PreHookResult OnFunctionPre(Span<ulong> args)
    {
        // FuncType argument
        var funcType = (int)args[2];

        if (funcType <= FunctionTypeRegistry.TrainingFuncTypeMax) return PreHookResult.Continue;

        if (!FunctionTypeRegistry.TryGetFunctionId(funcType, out var functionId)) return PreHookResult.Continue;

        if (CustomFunctions.TryGetValue(functionId, out var action))
        {
            var baseParam = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param>(args[3]);
            var viewData = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param.ViewData>(args[4]);

            if (baseParam == null || viewData == null)
            {
                API.LogWarning("Failed to retrieve baseParam or viewData in OnFunctionPre.");
                return PreHookResult.Continue;
            }

            // Invoke function
            s_returnValueForCustomFunction = action.Invoke(baseParam, viewData, (int)args[5]);

            s_handledCustomFunction = true;

            // Skip the game's original handler
            return PreHookResult.Skip;
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc), "Function", MethodHookType.Post)]
    public static void OnFunctionPost(ref ulong retval)
    {
        if (s_handledCustomFunction)
        {
            // output is Boolean = true
            retval = s_returnValueForCustomFunction ? 1UL : 0UL;

            // Reset for the next call
            s_handledCustomFunction = false;
        }
    }

    internal static void RegisterCustomOptionSelectFunction(Guid id, FunctionDelegate action)
    {
        // Generate a unique FuncType integer for this custom function

        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomOptionSelectFunctions.TryAdd(id, action);
        else
            throw new InvalidOperationException(
                $"Custom option select function '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomOptionSelectFunction(Guid id)
    {
        CustomOptionSelectFunctions.Remove(id, out var _);
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc),
        "OptionSelectFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)",
        MethodHookType.Pre)]
    public static PreHookResult OnOptionSelectFunctionPre(Span<ulong> args)
    {
        // FuncType argument
        var funcType = (int)args[2];

        if (!FunctionTypeRegistry.TryGetFunctionId(funcType, out var functionId)) return PreHookResult.Continue;

        // Adjust the FuncType to match our custom range
        if (CustomOptionSelectFunctions.TryGetValue(functionId!, out var action))
        {
            try
            {
                var rowIndex = (int)args[5];

                var baseParam = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param>(args[3]);
                var viewData = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param.ViewData>(args[4]);

                // Invoke function
                s_returnValueForCustomOptionSelectFunction = action.Invoke(baseParam, viewData, rowIndex);
            }
            catch (Exception ex)
            {
                API.LogError($"Exception in OnOptionSelectFunctionPre: {ex.Message}");
                return PreHookResult.Continue;
            }

            // end of debugging

            s_handledCustomOptionSelectFunction = true;

            // Skip the game's original handler
            return PreHookResult.Skip;
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc),
        "OptionSelectFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)",
        MethodHookType.Post)]
    public static void OnOptionSelectFunctionPost(ref ulong retval)
    {
        if (s_handledCustomOptionSelectFunction)
        {
            // output is Boolean, determines whether or not the menu should close.
            retval = s_returnValueForCustomOptionSelectFunction ? 1UL : 0UL;

            // Reset for the next call
            s_handledCustomOptionSelectFunction = false;
        }
    }


    internal static void RegisterCustomOptionXFunction(Guid id, FunctionDelegate action)
    {
        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomOptionXFunctions.TryAdd(id, action);
        else
            throw new InvalidOperationException(
                $"Custom option X function '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomOptionXFunction(Guid id)
    {
        CustomOptionXFunctions.Remove(id, out var _);
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc),
        "OptionXFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)",
        MethodHookType.Pre)]
    public static PreHookResult OnOptionXFunctionPre(Span<ulong> args)
    {
        var funcType = (int)args[2];

        if (!FunctionTypeRegistry.TryGetFunctionId(funcType, out var functionId)) return PreHookResult.Continue;

        if (CustomOptionXFunctions.TryGetValue(functionId, out var action))
        {
            try
            {
                var rowIndex = (int)args[5];
                var baseParam = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param>(args[3]);
                var viewData = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param.ViewData>(args[4]);
                s_returnValueForCustomOptionXFunction = action.Invoke(baseParam, viewData, rowIndex);
            }
            catch (Exception ex)
            {
                API.LogError($"Exception in OnOptionXFunctionPre: {ex.Message}");
                return PreHookResult.Continue;
            }

            s_handledCustomOptionXFunction = true;
            return PreHookResult.Skip;
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc),
        "OptionXFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)",
        MethodHookType.Post)]
    public static void OnOptionXFunctionPost(ref ulong retval)
    {
        if (s_handledCustomOptionXFunction)
        {
            retval = s_returnValueForCustomOptionXFunction ? 1UL : 0UL;
            s_handledCustomOptionXFunction = false;
        }
    }

    internal static void RegisterCustomOptionYFunction(Guid id, FunctionDelegate action)
    {
        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomOptionYFunctions.TryAdd(id, action);
        else
            throw new InvalidOperationException(
                $"Custom option Y function '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomOptionYFunction(Guid id)
    {
        CustomOptionYFunctions.Remove(id, out var _);
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc),
        "OptionYFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)",
        MethodHookType.Pre)]
    public static PreHookResult OnOptionYFunctionPre(Span<ulong> args)
    {
        var funcType = (int)args[2];

        if (!FunctionTypeRegistry.TryGetFunctionId(funcType, out var functionId)) return PreHookResult.Continue;

        if (CustomOptionYFunctions.TryGetValue(functionId, out var action))
        {
            try
            {
                var rowIndex = (int)args[5];
                var baseParam = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param>(args[3]);
                var viewData = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param.ViewData>(args[4]);
                s_returnValueForCustomOptionYFunction = action.Invoke(baseParam, viewData, rowIndex);
            }
            catch (Exception ex)
            {
                API.LogError($"Exception in OnOptionYFunctionPre: {ex.Message}");
                return PreHookResult.Continue;
            }

            s_handledCustomOptionYFunction = true;
            return PreHookResult.Skip;
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc),
        "OptionYFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)",
        MethodHookType.Post)]
    public static void OnOptionYFunctionPost(ref ulong retval)
    {
        if (s_handledCustomOptionYFunction)
        {
            retval = s_returnValueForCustomOptionYFunction ? 1UL : 0UL;
            s_handledCustomOptionYFunction = false;
        }
    }

    internal static void Clear()
    {
        CustomFunctions.Clear();
        CustomOptionSelectFunctions.Clear();
        CustomOptionXFunctions.Clear();
        CustomOptionYFunctions.Clear();
    }
}