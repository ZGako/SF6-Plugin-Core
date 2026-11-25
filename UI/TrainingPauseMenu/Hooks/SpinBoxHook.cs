using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

/// <summary>
///     Intercepts <see cref="app.training.UIFlowTrainingMenu.Param.InitSpinBox(int)" /> so custom or modified Training
///     Pause Menu spinners select the child option matching the mod's live backing state.
/// </summary>
public static class SpinBoxHook
{
    /// <summary>
    ///     Callback invoked before SF6 initializes a registered spinner row, returning the
    ///     <see cref="app.training.TrainingFuncType" /> or mod <see cref="Guid" /> of the child option that should be
    ///     displayed as selected.
    /// </summary>
    /// <param name="param">The active <see cref="app.training.UIFlowTrainingMenu.Param" /> building the spinner row.</param>
    /// <returns>
    ///     A <see cref="TrainingFuncOrGuid" /> identifying one of the spinner's child <c>FuncType</c> entries; its
    ///     0-based child index is written to <c>ViewDataList[rowIndex].Index</c>.
    /// </returns>
    public delegate TrainingFuncOrGuid SpinBoxInitDelegate(app.training.UIFlowTrainingMenu.Param param);

    // since the initialization of the spinbox is determined by the game's logic, it doesn't match with our modified/added spinboxes, so we need to dispatch custom initialization logic
    private static readonly ConcurrentDictionary<Guid, SpinBoxInitDelegate> CustomInitializations = [];

    internal static void RegisterCustomSpinBoxInitialization(Guid id, SpinBoxInitDelegate initDelegate)
    {
        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomInitializations[id] = initDelegate;
        else
            throw new InvalidOperationException(
                $"Custom spinbox initialization function '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomSpinBoxInitialization(Guid id)
    {
        CustomInitializations.TryRemove(id, out var _);
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.training.UIFlowTrainingMenu.Param.InitSpinBox(System.Int32)</c> (not intended
    ///     for direct calls by mods). Resolves the registered <see cref="SpinBoxInitDelegate" /> for the row at
    ///     <c>args[2]</c> and aligns <c>ViewData.Index</c> to the matching child entry.
    /// </summary>
    /// <param name="args">
    ///     Raw VM argument span: <c>args[1]</c> is <see cref="app.training.UIFlowTrainingMenu.Param" /> and
    ///     <c>args[2]</c> is the row index in <c>ViewDataList</c>.
    /// </param>
    /// <returns>Always <see cref="PreHookResult.Continue" /> so SF6 renders the spinner using the updated index.</returns>
    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "InitSpinBox(System.Int32)", MethodHookType.Pre)]
    public static PreHookResult OnInitSpinBoxPre(Span<ulong> args)
    {
        try
        {
            var currentObject = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param>(args[1]);

            if (currentObject == null)
            {
                API.LogWarning("Failed to retrieve current UIFlowTrainingMenu.Param object.");
                return PreHookResult.Continue;
            }

            var currentIndex = (int)args[2];

            // get the current spinbox
            if (currentIndex < 0 || currentIndex >= currentObject.ViewDataList.Count) return PreHookResult.Continue;

            var currentSpinBox = currentObject.ViewDataList[currentIndex].Data;

            if (!FunctionTypeRegistry.TryGetFunctionId((int)currentSpinBox.FuncType, out var functionId))
                return PreHookResult.Continue;

            if (CustomInitializations.TryGetValue(functionId, out var initDelegate))
            {
                // Invoke the custom initialization logic
                var returnVal = initDelegate.Invoke(currentObject);

                var selectedFuncType = returnVal switch
                {
                    TrainingFuncOrGuid { IsGuid: true, GuidValue: var guid } =>
                        FunctionTypeRegistry.TryGetFunctionType(guid, out var funcType_ret)
                            ? funcType_ret
                            : throw new InvalidOperationException(
                                $"Custom spinbox initialization delegate returned an unregistered function type ID '{guid}'."),
                    TrainingFuncOrGuid { IsGuid: false, IntValue: var intValue } =>
                        (int)intValue < FunctionTypeRegistry.TrainingFuncTypeMax
                            ? (int)intValue
                            : throw new InvalidOperationException(
                                $"Custom spinbox initialization delegate returned an invalid function type integer '{intValue}'.")
                };

                // search the childdata for the element of the selectedFuncType and set the index to that element
                for (var i = 0; i < currentSpinBox.ChildData.Count; i++)
                    if ((int)currentSpinBox.ChildData[i].FuncType == selectedFuncType)
                    {
                        currentObject.ViewDataList[currentIndex].Index = i;
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in SpinBoxHook.OnInitSpinBoxPre: {ex.Message}");
        }

        return PreHookResult.Continue;
    }

    internal static void Clear()
    {
        CustomInitializations.Clear();
    }
}