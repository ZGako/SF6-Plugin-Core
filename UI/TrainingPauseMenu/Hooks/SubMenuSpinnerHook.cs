using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Helpers;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

public static class SubMenuSpinnerHook
{
    public delegate (string outputText, bool isNotDefault) SubMenuSpinnerFormatDelegate(int value);

    public delegate int SubMenuSpinnerInitDelegate();

    public delegate (int Min, int Max) SubMenuSpinnerLimitDelegate();

    private static readonly
        ConcurrentDictionary<Guid, (
            SubMenuSpinnerInitDelegate InitDelegate,
            SubMenuSpinnerFormatDelegate FormatDelegate,
            SubMenuSpinnerLimitDelegate? LimitDelegate
            )>
        CustomSubMenuSpinners = [];

    private static readonly ManagedStringPool StringPool = new();

    [ThreadStatic] private static bool s_isInitializing;

    private static readonly ThreadLocal<Queue<app.training.UIFlowTrainingMenu.Param.ViewData>> PerThreadViewData =
        new(() => new Queue<app.training.UIFlowTrainingMenu.Param.ViewData>(14));

    // Since this is only affected by UI stuff, there is no thread-related race condition
    // (everything we deal with is serialized on a single thread).
    private static readonly OrderedDictionary<ulong, app.training.UIFlowTrainingMenu.Param.ViewData>
        ViewDataForUIPartAddress = new();

    [ThreadStatic] private static int s_pendingIndex;

    [ThreadStatic] private static ulong s_hasPendingUpdateAddress;

    [ThreadStatic] private static bool s_pendingIsAction;

    internal static void RegisterCustomSubMenuSpinner(
        Guid id,
        SubMenuSpinnerInitDelegate initDelegate,
        SubMenuSpinnerFormatDelegate formatDelegate,
        SubMenuSpinnerLimitDelegate? limitDelegate = null)
    {
        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomSubMenuSpinners.TryAdd(id, (initDelegate, formatDelegate, limitDelegate));
        else
            throw new InvalidOperationException(
                $"Custom submenu spinner '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomSubMenuSpinner(Guid id)
    {
        CustomSubMenuSpinners.TryRemove(id, out var _);
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu_SpinList.Param), "Initialize", MethodHookType.Pre)]
    public static PreHookResult OnSpinListInitializePre(Span<ulong> args)
    {
        // keep track when an element is being initialized
        s_isInitializing = true;

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu_SpinList.Param), "Initialize", MethodHookType.Post)]
    public static void OnSpinListInitializePost(ref ulong retval)
    {
        s_isInitializing = false;
    }

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
            // clear the dictionary as well (could be done in Initialize, but both get called in the same parts so it doesn't really matter)
            ViewDataForUIPartAddress.Clear();

            // explicit for loop as foreach sometimes breaks
            for (var i = 0; i < paramData.ViewDataList.Count; i++)
            {
                var viewData = paramData.ViewDataList[i];
                // add all viewDataList elements, we'll let the InitList function deal with them
                PerThreadViewData.Value?.Enqueue(viewData);
            }
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in SubMenuSpinnerHook.OnCreateViewDataPost: {ex.Message}");
        }
    }

    [MethodHook(typeof(app.UIPartsScrollList), "InitList", MethodHookType.Pre)]
    public static PreHookResult OnInitList(Span<ulong> args)
    {
        try
        {
            var address = args[1];
            // so if we are initializing
            if (s_isInitializing)
            {
                if (PerThreadViewData.Value == null || PerThreadViewData.Value.Count == 0)
                    return PreHookResult.Continue;

                // pop the queue, read its values and record the address mapping for all elements
                // so row indices stay aligned with ViewDataList even when preexisting game elements are present
                var viewData = PerThreadViewData.Value.Dequeue();
                ViewDataForUIPartAddress.TryAdd(address, viewData);

                var data = viewData.Data;
                if (!FunctionTypeRegistry.TryGetFunctionId((int)data.FuncType, out var functionId) ||
                    !CustomSubMenuSpinners.TryGetValue(functionId, out var callbacks))
                    return PreHookResult.Continue;

                if (callbacks.LimitDelegate != null)
                {
                    var (min, max) = callbacks.LimitDelegate.Invoke();
                    var limit = via.Int2.REFType.CreateValueType().As<via.Int2>();
                    limit.x = min;
                    limit.y = max;
                    data.LimitValue = limit;
                }

                var maxValues = data.LimitValue.y - data.LimitValue.x + 1;
                var initValue = callbacks.InitDelegate.Invoke();
                var mappedInitIndex = Math.Clamp(initValue - data.LimitValue.x, 0, maxValues - 1);

                viewData.Index = mappedInitIndex;
                args[2] = (ulong)maxValues;
                args[3] = (ulong)mappedInitIndex;
            }
            else
            {
                // in the case where we're not initializing (meaning it is the changing focus operation) we compare the address to the dictionary we have.
                if (ViewDataForUIPartAddress.TryGetValue(address, out var viewData))
                {
                    var data = viewData.Data;
                    if (!FunctionTypeRegistry.TryGetFunctionId((int)data.FuncType, out var functionId) ||
                        !CustomSubMenuSpinners.TryGetValue(functionId, out var callbacks))
                        return PreHookResult.Continue;

                    if (callbacks.LimitDelegate != null)
                    {
                        var (min, max) = callbacks.LimitDelegate.Invoke();
                        var limit = via.Int2.REFType.CreateValueType().As<via.Int2>();
                        limit.x = min;
                        limit.y = max;
                        data.LimitValue = limit;
                    }

                    var maxValues = data.LimitValue.y - data.LimitValue.x + 1;
                    viewData.Index = Math.Clamp(viewData.Index, 0, maxValues - 1);
                    args[2] = (ulong)maxValues;
                    args[3] = (ulong)viewData.Index;
                }
            }
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in SubMenuSpinnerHook.OnInitList: {ex.Message}");
        }

        return PreHookResult.Continue;
    }

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

    [MethodHook(typeof(app.training.UIFlowTrainingMenu_SpinList.Param), "UpdateSpinBox(System.Int32, System.Boolean)",
        MethodHookType.Post)]
    public static void OnUpdateSpinBoxPost(ref ulong retval)
    {
        if (s_hasPendingUpdateAddress == 0) return;

        var param = ManagedObject.FromAddress(s_hasPendingUpdateAddress)
            .As<app.training.BaseParam>();
        s_hasPendingUpdateAddress = 0; // Always clear to prevent stale state on subsequent calls

        try
        {
            if (s_pendingIndex < 0 || s_pendingIndex >= ViewDataForUIPartAddress.Count) return;
            var (address, viewData) = ViewDataForUIPartAddress.GetAt(s_pendingIndex);
            var data = viewData.Data;

            if (!FunctionTypeRegistry.TryGetFunctionId((int)data.FuncType, out var functionId) ||
                !CustomSubMenuSpinners.TryGetValue(functionId, out var callbacks))
                return;

            var actualValue = viewData.Index + data.LimitValue.x;

            if (s_pendingIsAction)
                // here call the training menu func with the offset-adjusted value
                GameSingletonManager.TrainingManager!._UIFunc.Function(data.FuncType, param, viewData,
                    actualValue);

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
        ViewDataForUIPartAddress.Clear();
        PerThreadViewData.Value?.Clear();
        StringPool.Clear();
    }
}