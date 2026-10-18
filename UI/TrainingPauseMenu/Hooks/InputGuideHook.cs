using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

public static class InputGuideHook
{
    // delegate type for pause menu input guide data modification
    public delegate void InputGuideDelegate(
        ref REFrameworkNET.Collections.IList<app.InputGuideData> outInputGuideDataList,
        ref REFrameworkNET.Collections.IList<string> outStringList);

    private static readonly ConcurrentDictionary<Guid, InputGuideDelegate> CustomInputGuideDelegates = [];


    [ThreadStatic] private static ulong s_pendingOutInputGuideDataListPtr;

    [ThreadStatic] private static ulong s_pendingOutStringListPtr;

    [ThreadStatic] private static int s_handledCustomFunction;


    internal static void RegisterCustomInputGuideFunction(Guid id, InputGuideDelegate action)
    {
        // Generate a unique FuncType integer for this custom function

        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomInputGuideDelegates.TryAdd(id, action);
        else
            throw new InvalidOperationException(
                $"Custom input guide function '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomInputGuideFunction(Guid id)
    {
        CustomInputGuideDelegates.TryRemove(id, out var _);
    }


    [MethodHook(typeof(app.training.TrainingManager),
        "GetGuideData(app.training.TrainingMenuData, System.Collections.Generic.List`1<app.InputGuideData>, System.Collections.Generic.List`1<System.String>)",
        MethodHookType.Pre)]
    public static PreHookResult OnGetGuideDataPre(Span<ulong> args)
    {
        var currentMenuData = ManagedObject.ToManagedObject(args[2])?.As<app.training.TrainingMenuData>();

        if (currentMenuData == null)
        {
            API.LogWarning("Failed to retrieve current TrainingMenuData object.");
            return PreHookResult.Continue;
        }

        if (!FunctionTypeRegistry.TryGetFunctionId((int)currentMenuData.FuncType, out var id))
            return PreHookResult.Continue;

        if (CustomInputGuideDelegates.ContainsKey(id))
        {
            s_handledCustomFunction = (int)currentMenuData.FuncType;
            s_pendingOutInputGuideDataListPtr = args[3];
            s_pendingOutStringListPtr = args[4];
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.TrainingManager),
        "GetGuideData(app.training.TrainingMenuData, System.Collections.Generic.List`1<app.InputGuideData>, System.Collections.Generic.List`1<System.String>)",
        MethodHookType.Post)]
    public static void OnGetGuideDataPost(ref ulong retval)
    {
        if (s_pendingOutInputGuideDataListPtr == 0 || s_pendingOutStringListPtr == 0 ||
            s_handledCustomFunction == 0) return;

        if (!FunctionTypeRegistry.TryGetFunctionId(s_handledCustomFunction, out var id)) return;

        if (CustomInputGuideDelegates.TryGetValue(id, out var action))
            try
            {
                // Safely dereference the pointer using Marshal
                var dereferencedAddress1 =
                    (ulong)Marshal.ReadInt64(new IntPtr((long)s_pendingOutInputGuideDataListPtr));
                var dereferencedAddress2 = (ulong)Marshal.ReadInt64(new IntPtr((long)s_pendingOutStringListPtr));

                var inputGuideDataList =
                    MemoryHelpers.GetAddressAs<REFrameworkNET.Collections.IList<app.InputGuideData>>(
                        dereferencedAddress1);
                var outStringList =
                    MemoryHelpers.GetAddressAs<REFrameworkNET.Collections.IList<string>>(dereferencedAddress2);

                action.Invoke(ref inputGuideDataList, ref outStringList);
            }
            catch (Exception ex)
            {
                API.LogError($"Error occurred while retrieving the input guide data list: {ex.Message}");
                return;
            }

        s_handledCustomFunction = 0; // Reset after use
        s_pendingOutInputGuideDataListPtr = 0; // Reset after use
        s_pendingOutStringListPtr = 0; // Reset after use
    }

    internal static void Clear()
    {
        CustomInputGuideDelegates.Clear();
    }
}