using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

/// <summary>
///     Intercepts <c>app.training.TrainingManager.GetGuideData</c> so mods can append or customize bottom-bar input
///     guide entries shown when focusing a vanilla or custom Training Pause Menu item.
/// </summary>
public static class InputGuideHook
{
    /// <summary>
    ///     Callback invoked after SF6 populates the bottom input guide lists for a registered item, allowing mods to
    ///     add <see cref="app.InputGuideData" /> items or custom guide strings.
    /// </summary>
    /// <param name="outInputGuideDataList">Engine list of structured input-prompt descriptors rendered in the footer bar.</param>
    /// <param name="outStringList">Parallel engine string list populated alongside <paramref name="outInputGuideDataList" />.</param>
    public delegate void InputGuideDelegate(
        ref REFrameworkNET.Collections.IList<app.InputGuideData> outInputGuideDataList,
        ref REFrameworkNET.Collections.IList<string> outStringList);

    private static readonly ConcurrentDictionary<Guid, InputGuideDelegate> CustomInputGuideDelegates = [];


    [ThreadStatic]
    private static Stack<(int FuncType, ulong GuideDataListPtr, ulong StringListPtr)>? s_pendingCalls;


    internal static void RegisterCustomInputGuideFunction(Guid id, InputGuideDelegate action)
    {
        // Generate a unique FuncType integer for this custom function

        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomInputGuideDelegates[id] = action;
        else
            throw new InvalidOperationException(
                $"Custom input guide function '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomInputGuideFunction(Guid id)
    {
        CustomInputGuideDelegates.TryRemove(id, out var _);
    }


    /// <summary>
    ///     REFramework pre-hook on <c>app.training.TrainingManager.GetGuideData</c> (not intended for direct calls by
    ///     mods). Pushes the target <c>FuncType</c> and output list pointer addresses onto a thread-local stack if a
    ///     custom guide delegate is registered for the item.
    /// </summary>
    /// <param name="args">
    ///     Raw VM argument span: <c>args[2]</c> is <see cref="app.training.TrainingMenuData" />, <c>args[3]</c> is the
    ///     <c>List&lt;InputGuideData&gt;</c> ref/out slot, and <c>args[4]</c> is the <c>List&lt;string&gt;</c> slot.
    /// </param>
    /// <returns>Always <see cref="PreHookResult.Continue" /> so vanilla guide items are populated first.</returns>
    [MethodHook(typeof(app.training.TrainingManager),
        "GetGuideData(app.training.TrainingMenuData, System.Collections.Generic.List`1<app.InputGuideData>, System.Collections.Generic.List`1<System.String>)",
        MethodHookType.Pre)]
    public static PreHookResult OnGetGuideDataPre(Span<ulong> args)
    {
        s_pendingCalls ??= new Stack<(int, ulong, ulong)>();
        var callState = (0, 0UL, 0UL);

        try
        {
            var currentMenuData = ManagedObject.ToManagedObject(args[2])?.As<app.training.TrainingMenuData>();
            if (currentMenuData == null)
            {
                API.LogWarning("Failed to retrieve current TrainingMenuData object.");
            }
            else if (FunctionTypeRegistry.TryGetFunctionId((int)currentMenuData.FuncType, out var id) &&
                     CustomInputGuideDelegates.ContainsKey(id))
            {
                callState = ((int)currentMenuData.FuncType, args[3], args[4]);
            }
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in InputGuideHook.OnGetGuideDataPre: {ex.Message}");
        }

        s_pendingCalls.Push(callState);
        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework post-hook on <c>app.training.TrainingManager.GetGuideData</c> (not intended for direct calls by
    ///     mods). Dereferences the captured list pointers and invokes the registered <see cref="InputGuideDelegate" />.
    /// </summary>
    /// <param name="retval">Raw return value from the hooked method (unused).</param>
    [MethodHook(typeof(app.training.TrainingManager),
        "GetGuideData(app.training.TrainingMenuData, System.Collections.Generic.List`1<app.InputGuideData>, System.Collections.Generic.List`1<System.String>)",
        MethodHookType.Post)]
    public static void OnGetGuideDataPost(ref ulong retval)
    {
        if (s_pendingCalls == null || !s_pendingCalls.TryPop(out var callState)) return;

        var (handledCustomFunction, outInputGuideDataListPtr, outStringListPtr) = callState;
        if (handledCustomFunction == 0 || outInputGuideDataListPtr == 0 || outStringListPtr == 0) return;

        if (!FunctionTypeRegistry.TryGetFunctionId(handledCustomFunction, out var id)) return;

        if (CustomInputGuideDelegates.TryGetValue(id, out var action))
            try
            {
                // Safely dereference the pointer using Marshal
                var dereferencedAddress1 =
                    (ulong)Marshal.ReadInt64(new IntPtr((long)outInputGuideDataListPtr));
                var dereferencedAddress2 = (ulong)Marshal.ReadInt64(new IntPtr((long)outStringListPtr));

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
            }
    }

    internal static void Clear()
    {
        CustomInputGuideDelegates.Clear();
    }
}