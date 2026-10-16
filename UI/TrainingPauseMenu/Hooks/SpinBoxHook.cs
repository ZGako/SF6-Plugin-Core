using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

public static class SpinBoxHook
{

    /// <summary>
    /// Delegate type for custom spinbox initialization logic. This should return the FunctionName of the selected option for the spinbox.
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public delegate TrainingFuncOrGuid SpinBoxInitDelegate(app.training.UIFlowTrainingMenu.Param param);

    // since the initialization of the spinbox is determined by the game's logic, it doesn't match with our modified/added spinboxes, so we need to dispatch custom initialization logic
    private static readonly ConcurrentDictionary<Guid, SpinBoxInitDelegate> CustomInitializations = [];

    internal static void RegisterCustomSpinBoxInitialization(Guid id, SpinBoxInitDelegate initDelegate)
    {
        if (FunctionTypeRegistry.TryGetFunctionType(id, out int _))
        {
            CustomInitializations.TryAdd(id, initDelegate);
        }
        else
        {
            throw new InvalidOperationException($"Custom spinbox initialization function '{id}' is not registered. Call RegisterNewFunctionType first.");
        }
    }

    internal static void UnregisterCustomSpinBoxInitialization(Guid id)
    {
        CustomInitializations.TryRemove(id, out SpinBoxInitDelegate? _);
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "InitSpinBox(System.Int32)", MethodHookType.Pre)]
    public static PreHookResult OnInitSpinBoxPre(Span<ulong> args)
    {
        var currentObject = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param>(args[1]);

        if (currentObject == null)
        {
            API.LogWarning("Failed to retrieve current UIFlowTrainingMenu.Param object.");
            return PreHookResult.Continue;
        }

        int currentIndex = (int)args[2];

        // get the current spinbox
        if (currentIndex < 0 || currentIndex >= currentObject.ViewDataList.Count)
        {
            return PreHookResult.Continue;
        }

        var currentSpinBox = currentObject.ViewDataList[currentIndex].Data;

        if (!FunctionTypeRegistry.TryGetFunctionId((int)currentSpinBox.FuncType, out Guid functionId))
        {
            return PreHookResult.Continue;
        }

        if (CustomInitializations.TryGetValue(functionId, out SpinBoxInitDelegate? initDelegate))
        {
            // Invoke the custom initialization logic
            TrainingFuncOrGuid returnVal = initDelegate.Invoke(currentObject);

            int selectedFuncType = returnVal switch
            {
                TrainingFuncOrGuid { IsGuid: true, GuidValue: var guid } =>
                    FunctionTypeRegistry.TryGetFunctionType(guid, out int funcType_ret) ? funcType_ret :
                    throw new InvalidOperationException($"Custom spinbox initialization delegate returned an unregistered function type ID '{guid}'."),
                TrainingFuncOrGuid { IsGuid: false, IntValue: var intValue } =>
                    (int)intValue < FunctionTypeRegistry.TrainingFuncTypeMax ? (int)intValue :
                    throw new InvalidOperationException($"Custom spinbox initialization delegate returned an invalid function type integer '{intValue}'.")
            };

            // search the childdata for the element of the selectedFuncType and set the index to that element
            for (int i = 0; i < currentSpinBox.ChildData.Count; i++)
            {
                if ((int)currentSpinBox.ChildData[i].FuncType == selectedFuncType)
                {
                    currentObject.ViewDataList[currentIndex].Index = i;
                    break;
                }
            }
        }

        return PreHookResult.Continue;
    }

    internal static void Clear()
    {
        CustomInitializations.Clear();
    }

}