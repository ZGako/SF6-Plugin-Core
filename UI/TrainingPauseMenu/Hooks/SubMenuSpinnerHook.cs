using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Helpers;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

/// <summary>
///     Drives custom numeric/formatted spinner rows inside SF6's <c>SPINLIST</c> modal submenu
///     (<c>app.training.UIFlowTrainingMenu_SpinList</c>), supporting dynamic range limits, live value formatting,
///     conditional row hiding, and offset-adjusted value dispatch.
/// </summary>
public static class SubMenuSpinnerHook
{
    /// <summary>
    ///     Optional filter evaluated when a <c>SPINLIST</c> submenu creates its <c>ViewDataList</c>. Returning
    ///     <see langword="true" /> omits this spinner row from the popup entirely.
    /// </summary>
    /// <returns><see langword="true" /> to hide this row from the current submenu popup; <see langword="false" /> to keep it.</returns>
    public delegate bool MaskThisItemRowDelegate();

    /// <summary>
    ///     Formats the actual integer value (<c>Index + LimitValue.x</c>) for display inside the submenu spinner and
    ///     indicates whether it differs from its default (non-default values render in SF6's accent color).
    /// </summary>
    /// <param name="value">Actual domain integer within <c>[LimitValue.x, LimitValue.y]</c>.</param>
    /// <returns>
    ///     A tuple of the rendered display text and <c>isNotDefault</c> (which applies
    ///     <c>&lt;COLOR preset="FGM_Accent"&gt;</c> when <see langword="true" /> and the row is enabled).
    /// </returns>
    public delegate (string outputText, bool isNotDefault) SubMenuSpinnerFormatDelegate(int value);

    /// <summary>
    ///     Supplies the spinner's current backing integer value during submenu open and non-action list refreshes.
    /// </summary>
    /// <returns>Current value in domain units (clamped to <c>[LimitValue.x, LimitValue.y]</c> before mapping to scroll index).</returns>
    public delegate int SubMenuSpinnerInitDelegate();

    /// <summary>
    ///     Optional dynamic bounds provider evaluated on submenu initialization and non-action <c>UpdateList</c>
    ///     refreshes, updating native <c>TrainingMenuData.LimitValue</c> before mapping <see cref="SubMenuSpinnerInitDelegate" />.
    /// </summary>
    /// <returns>Inclusive <c>(Min, Max)</c> domain range.</returns>
    public delegate (int Min, int Max) SubMenuSpinnerLimitDelegate();

    private static readonly
        ConcurrentDictionary<Guid, (
            SubMenuSpinnerInitDelegate InitDelegate,
            SubMenuSpinnerFormatDelegate FormatDelegate,
            SubMenuSpinnerLimitDelegate? LimitDelegate,
            MaskThisItemRowDelegate? MaskThisItemRow
            )>
        CustomSubMenuSpinners = [];

    private static readonly ManagedStringPool StringPool = new();

    [ThreadStatic] private static bool s_isInitializing;

    private static readonly ThreadLocal<Queue<app.training.UIFlowTrainingMenu.Param.ViewData>> PerThreadViewData =
        new(() => new Queue<app.training.UIFlowTrainingMenu.Param.ViewData>(14));

    private static readonly Lock ViewDataLock = new();

    private static readonly OrderedDictionary<ulong, app.training.UIFlowTrainingMenu.Param.ViewData>
        ViewDataForUIPartAddress = new();

    [ThreadStatic] private static int s_pendingIndex;

    [ThreadStatic] private static ulong s_hasPendingUpdateAddress;

    [ThreadStatic] private static bool s_pendingIsAction;

    internal static void RegisterCustomSubMenuSpinner(
        Guid id,
        SubMenuSpinnerInitDelegate initDelegate,
        SubMenuSpinnerFormatDelegate formatDelegate,
        SubMenuSpinnerLimitDelegate? limitDelegate = null,
        MaskThisItemRowDelegate? maskThisItemRow = null)
    {
        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomSubMenuSpinners[id] = (initDelegate, formatDelegate, limitDelegate, maskThisItemRow);
        else
            throw new InvalidOperationException(
                $"Custom submenu spinner '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomSubMenuSpinner(Guid id)
    {
        CustomSubMenuSpinners.TryRemove(id, out var _);
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.training.UIFlowTrainingMenu_SpinList.Param.Initialize</c> (not intended for
    ///     direct calls by mods). Marks initialization active so <see cref="OnCreateViewDataPost" /> and
    ///     <see cref="OnInitList" /> capture and correlate all row scroll-list pointers in order.
    /// </summary>
    /// <param name="args">Raw VM argument span (unused).</param>
    /// <returns>Always <see cref="PreHookResult.Continue" />.</returns>
    [MethodHook(typeof(app.training.UIFlowTrainingMenu_SpinList.Param), "Initialize", MethodHookType.Pre)]
    public static PreHookResult OnSpinListInitializePre(Span<ulong> args)
    {
        // keep track when an element is being initialized
        s_isInitializing = true;

        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework post-hook on <c>app.training.UIFlowTrainingMenu_SpinList.Param.Initialize</c> (not intended for
    ///     direct calls by mods). Ends the initialization capture phase.
    /// </summary>
    /// <param name="retval">Raw return value from the hooked method (unused).</param>
    [MethodHook(typeof(app.training.UIFlowTrainingMenu_SpinList.Param), "Initialize", MethodHookType.Post)]
    public static void OnSpinListInitializePost(ref ulong retval)
    {
        s_isInitializing = false;
    }

    /// <summary>
    ///     REFramework post-hook on <c>app.training.UIFlowTrainingMenu_SpinList.Param.CreateViewData</c> (not intended
    ///     for direct calls by mods). Removes masked custom rows from <c>ViewDataList</c> and queues the surviving
    ///     <c>ViewData</c> entries for subsequent <see cref="OnInitList" /> pairing.
    /// </summary>
    /// <param name="retval">Raw return value from the hooked method (unused).</param>
    [MethodHook(typeof(app.training.UIFlowTrainingMenu_SpinList.Param), "CreateViewData", MethodHookType.Post)]
    public static void OnCreateViewDataPost(ref ulong retval)
    {
        if (!s_isInitializing) return;
        try
        {
            // we check the view data in the post, to make sure that our "custom" viewData is present
            var tm = GameSingletonManager.TrainingManager ??
                     throw new NullReferenceException("TrainingManager not found");

            var paramData = ((tm._UITrainingMenu_SpinList as IObject)?.GetField("_ParamData") as ManagedObject)
                            ?.As<app.training.UIFlowTrainingMenu_SpinList.Param>() ??
                            throw new NullReferenceException(
                                "Could not get field _ParamData of UITrainingMenu_SpinList");

            // clear the list just in case something in the flow broke
            PerThreadViewData.Value?.Clear();
            lock (ViewDataLock)
            {
                ViewDataForUIPartAddress.Clear();
            }

            // explicit for loop as foreach sometimes breaks
            for (var i = 0; i < paramData.ViewDataList.Count; i++)
            {
                var viewData = paramData.ViewDataList[i];
                var data = viewData.Data;

                if (FunctionTypeRegistry.TryGetFunctionId((int)data.FuncType, out var functionId) &&
                    CustomSubMenuSpinners.TryGetValue(functionId, out var callbacks) &&
                    callbacks.MaskThisItemRow?.Invoke() == true)
                {
                    paramData.ViewDataList.RemoveAt(i);
                    i--;
                    continue;
                }

                // add all viewDataList elements, we'll let the InitList function deal with them
                PerThreadViewData.Value?.Enqueue(viewData);
            }
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in SubMenuSpinnerHook.OnCreateViewDataPost: {ex.Message}");
        }
    }

    private static (int maxValues, int mappedIndex, int actualValue) SyncViewData(
        app.training.UIFlowTrainingMenu.Param.ViewData viewData,
        SubMenuSpinnerInitDelegate initDelegate,
        SubMenuSpinnerLimitDelegate? limitDelegate)
    {
        var data = viewData.Data;

        if (limitDelegate != null)
        {
            var (min, max) = limitDelegate.Invoke();
            var limit = via.Int2.REFType.CreateValueType().As<via.Int2>();
            limit.x = min;
            limit.y = max;
            data.LimitValue = limit;
        }

        var maxValues = Math.Max(1, data.LimitValue.y - data.LimitValue.x + 1);
        var currentValue = initDelegate.Invoke();
        var mappedIndex = Math.Clamp(currentValue - data.LimitValue.x, 0, maxValues - 1);

        viewData.Index = mappedIndex;
        return (maxValues, mappedIndex, mappedIndex + data.LimitValue.x);
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.UIPartsScrollList.InitList</c> (not intended for direct calls by mods). Pairs
    ///     each scroll-list instance with its <c>ViewData</c> and patches <c>args[2]</c>/<c>args[3]</c> (count and
    ///     selected index) for registered custom submenu spinners.
    /// </summary>
    /// <param name="args">
    ///     Raw VM argument span: <c>args[1]</c> is <see cref="app.UIPartsScrollList" />, <c>args[2]</c> is total option
    ///     count, and <c>args[3]</c> is initial 0-based scroll index.
    /// </param>
    /// <returns>Always <see cref="PreHookResult.Continue" />.</returns>
    [MethodHook(typeof(app.UIPartsScrollList), "InitList", MethodHookType.Pre)]
    public static PreHookResult OnInitList(Span<ulong> args)
    {
        try
        {
            var address = args[1];
            app.training.UIFlowTrainingMenu.Param.ViewData? viewData;

            if (s_isInitializing)
            {
                if (PerThreadViewData.Value == null || PerThreadViewData.Value.Count == 0)
                    return PreHookResult.Continue;

                // pop the queue, read its values and record the address mapping for all elements
                // so row indices stay aligned with ViewDataList even when preexisting game elements are present
                viewData = PerThreadViewData.Value.Dequeue();
                lock (ViewDataLock)
                {
                    ViewDataForUIPartAddress.TryAdd(address, viewData);
                }
            }
            else
            {
                lock (ViewDataLock)
                {
                    if (!ViewDataForUIPartAddress.TryGetValue(address, out viewData))
                        return PreHookResult.Continue;
                }
            }

            var data = viewData.Data;
            if (!FunctionTypeRegistry.TryGetFunctionId((int)data.FuncType, out var functionId) ||
                !CustomSubMenuSpinners.TryGetValue(functionId, out var callbacks))
                return PreHookResult.Continue;

            var (maxValues, mappedIndex, _) = SyncViewData(viewData, callbacks.InitDelegate, callbacks.LimitDelegate);
            args[2] = (ulong)maxValues;
            args[3] = (ulong)mappedIndex;
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in SubMenuSpinnerHook.OnInitList: {ex.Message}");
        }

        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.UIPartsSpin.set_IsDisable</c> (not intended for direct calls by mods).
    ///     Applies dimmed arrow/background overlays (<c>e_tex_sub_custom</c> and arrow panels) while <c>SPINLIST</c>
    ///     rows transition between enabled and disabled states.
    /// </summary>
    /// <param name="args">Raw VM argument span where <c>args[1]</c> is <see cref="app.UIPartsSpin" /> and <c>args[2]</c> is boolean <c>isDisable</c>.</param>
    /// <returns>Always <see cref="PreHookResult.Continue" />.</returns>
    [MethodHook(typeof(app.UIPartsSpin), "set_IsDisable", MethodHookType.Pre)]
    public static PreHookResult OnSetDisablePre(Span<ulong> args)
    {
        try
        {
            var tm = GameSingletonManager.TrainingManager;
            if (tm == null) return PreHookResult.Continue;

            var boxedResult = (tm._UITrainingMenu_SpinList as IObject)?.GetField("_IsShowed");

            var boxedResult2 = (tm._UITrainingMenu_SpinList as IObject)?.GetField("_IsShowReq");
            var isShowed = (boxedResult != null && (bool)boxedResult) || (boxedResult2 != null && (bool)boxedResult2);
            if (!isShowed) return PreHookResult.Continue;

            var isDisable = args[2] == 1;
            if (args[1] == 0 || !ManagedObject.IsManagedObject(args[1])) return PreHookResult.Continue;
            var parts = ManagedObject.FromAddress(args[1]).As<app.UIPartsSpin>();
            var selectItem = parts._SelectItem;

            const string customTextureName = "e_tex_sub_custom";
            via.gui.Texture? customTexture = null;
            try
            {
                customTexture = selectItem.GetGuiChild(customTextureName).As<via.gui.Texture>();
            }
            catch (InvalidOperationException e)
            {
            }

            if (customTexture == null)
            {
                var texture = selectItem.GetGuiChild("e_tex_sub").As<via.gui.Texture>();
                if (texture == null) return PreHookResult.Continue;

                var duplicatedElement = texture.duplicate(customTextureName);
                customTexture = ManagedProxy<via.gui.Texture>.Create(duplicatedElement);

                var newColorVt = REFrameworkNET.ValueType.New<via.Color>();
                newColorVt.rgba = 0xFF720096;
                customTexture.Color = newColorVt;

                selectItem.addChild(duplicatedElement);
            }

            customTexture.Visible = isDisable;

            foreach (var (panelName, suffix) in new[]
                         { ("p_MouseDecideOperable_R", "R"), ("p_MouseDecideOperable_L", "L") })
            {
                var customPanelName = $"p_MouseDecideOperable_{suffix}_custom";

                via.gui.Panel? customPanel = null;
                try
                {
                    customPanel = selectItem.GetGuiChild(customPanelName).As<via.gui.Panel>();
                }
                catch (InvalidOperationException)
                {
                }

                if (customPanel == null)
                {
                    var panel = selectItem.GetGuiChild(panelName).As<via.gui.Panel>();
                    if (panel == null) continue;

                    var duplicatedPanel = panel.duplicate(customPanelName);
                    customPanel = ManagedProxy<via.gui.Panel>.Create(duplicatedPanel);

                    var arrow = customPanel.GetGuiChild("e_tex_arrow").As<via.gui.Texture>();
                    if (arrow != null)
                    {
                        var newColorVt = REFrameworkNET.ValueType.New<via.Color>();
                        newColorVt.rgba = 0x1AFFFFFF;
                        arrow.Color = newColorVt;
                    }

                    selectItem.addChild(duplicatedPanel);
                }

                customPanel.Priority = 1;

                customPanel.Visible = isDisable;
            }
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in SubMenuSpinnerHook.OnSetDisablePre: {ex.Message}");
        }

        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework pre-hook on <c>app.training.UIFlowTrainingMenu_SpinList.Param.UpdateSpinBox(System.Int32, System.Boolean)</c>
    ///     (not intended for direct calls by mods). Stashes the param pointer, row index, and <c>isAction</c> flag for
    ///     <see cref="OnUpdateSpinBoxPost" />.
    /// </summary>
    /// <param name="args">
    ///     Raw VM argument span: <c>args[1]</c> is <c>UIFlowTrainingMenu_SpinList.Param</c>, <c>args[2]</c> is row
    ///     index, and <c>args[3]</c> is boolean <c>isAction</c>.
    /// </param>
    /// <returns>Always <see cref="PreHookResult.Continue" />.</returns>
    [MethodHook(typeof(app.training.UIFlowTrainingMenu_SpinList.Param), "UpdateSpinBox(System.Int32, System.Boolean)",
        MethodHookType.Pre)]
    public static PreHookResult OnUpdateSpinBoxPre(Span<ulong> args)
    {
        // args[2] corresponds to the first parameter of the method
        s_pendingIndex = (int)args[2];
        s_pendingIsAction = args[3] == 1;
        // use the address as a way to self determine if you have a pending something (as address of 0 is unusable)
        s_hasPendingUpdateAddress = args[1];

        return PreHookResult.Continue;
    }

    /// <summary>
    ///     REFramework post-hook on <c>app.training.UIFlowTrainingMenu_SpinList.Param.UpdateSpinBox(System.Int32, System.Boolean)</c>
    ///     (not intended for direct calls by mods). Dispatches <c>_UIFunc.Function</c> with offset-adjusted domain
    ///     values on user actions (or re-syncs limits/index on non-action refreshes) and updates <c>e_txt_0</c> with
    ///     <see cref="SubMenuSpinnerFormatDelegate" /> output.
    /// </summary>
    /// <param name="retval">Raw return value from the hooked method (unused).</param>
    [MethodHook(typeof(app.training.UIFlowTrainingMenu_SpinList.Param), "UpdateSpinBox(System.Int32, System.Boolean)",
        MethodHookType.Post)]
    public static void OnUpdateSpinBoxPost(ref ulong retval)
    {
        if (s_hasPendingUpdateAddress == 0) return;

        var paramAddress = s_hasPendingUpdateAddress;
        var pendingIndex = s_pendingIndex;
        var pendingIsAction = s_pendingIsAction;
        s_hasPendingUpdateAddress = 0; // Always clear to prevent stale state on subsequent calls

        try
        {
            if (!ManagedObject.IsManagedObject(paramAddress)) return;
            var param = ManagedObject.FromAddress(paramAddress)
                .As<app.training.BaseParam>();

            ulong address;
            app.training.UIFlowTrainingMenu.Param.ViewData viewData;
            lock (ViewDataLock)
            {
                if (pendingIndex < 0 || pendingIndex >= ViewDataForUIPartAddress.Count) return;
                (address, viewData) = ViewDataForUIPartAddress.GetAt(pendingIndex);
            }

            var data = viewData.Data;

            if (!FunctionTypeRegistry.TryGetFunctionId((int)data.FuncType, out var functionId) ||
                !CustomSubMenuSpinners.TryGetValue(functionId, out var callbacks))
                return;

            int actualValue;
            if (pendingIsAction)
            {
                actualValue = viewData.Index + data.LimitValue.x;
                // here call the training menu func with the offset-adjusted value
                GameSingletonManager.TrainingManager!._UIFunc.Function(data.FuncType, param, viewData,
                    actualValue);
            }
            else
            {
                // Keep LimitValue and viewData.Index synced with the backing value during non-action refreshes
                // (e.g. Initialize, focus changes, or UpdateList() after another row changes bounds/mode).
                actualValue = SyncViewData(viewData, callbacks.InitDelegate, callbacks.LimitDelegate).actualValue;
            }

            if (address == 0 || !ManagedObject.IsManagedObject(address)) return;
            var partsScrollList = ManagedObject.FromAddress(address).As<app.UIPartsScrollList>();
            var selectItemText = partsScrollList._List.GetGuiChild("c_selectItem").As<via.gui.SelectItem>();
            var textItem = selectItemText.GetGuiChild("e_txt_0").As<via.gui.Text>();

            var (formattedText, isNotDefault) = callbacks.FormatDelegate.Invoke(actualValue);

            if (!data.IsEnabled)
                formattedText = $"<COLOR 8F82A7>{formattedText}</COLOR>";
            else
                formattedText = isNotDefault ? $"<COLOR preset=\"FGM_Accent\">{formattedText}</COLOR>" : formattedText;


            var str = StringPool.GetOrCreate(formattedText);

            (textItem as IObject)?.Call("set_Message", str);
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in SubMenuSpinnerHook.OnUpdateSpinBoxPost: {ex.Message}");
        }
    }

    internal static void Clear()
    {
        CustomSubMenuSpinners.Clear();
        lock (ViewDataLock)
        {
            ViewDataForUIPartAddress.Clear();
        }

        PerThreadViewData.Value?.Clear();
        StringPool.Clear();
    }
}