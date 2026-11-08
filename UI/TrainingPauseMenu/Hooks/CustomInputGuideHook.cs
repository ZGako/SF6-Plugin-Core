using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Helpers;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Hooks;

public static class CustomInputGuideHook
{
    public const float DefaultCustomTextBaseX = 560f;
    private const int AnimationFrameCount = 20;

    private static readonly ConcurrentDictionary<Guid, (app.InputAssign.Digital.Id InputId, float XPos)>
        CustomSecondaryListEntries = [];

    private static readonly ManagedStringPool StringPool = new();

    private static via.gui.Panel? s_previousCustomPanel;
    private static via.gui.Text? s_referenceNameText;
    private static via.gui.Text? s_activeCustomText;
    private static float s_referenceInitialX;
    private static float s_customTextInitialX;
    private static int s_remainingAnimationFrames;

    internal static void RegisterCustomSecondaryList(
        Guid id,
        app.InputAssign.Digital.Id inputId,
        float xPos = DefaultCustomTextBaseX)
    {
        if (FunctionTypeRegistry.TryGetFunctionType(id, out var _))
            CustomSecondaryListEntries.TryAdd(id, (inputId, xPos));
        else
            throw new InvalidOperationException(
                $"Custom secondary list '{id}' is not registered. Call RegisterNewFunctionType first.");
    }

    internal static void UnregisterCustomSecondaryList(Guid id)
    {
        CustomSecondaryListEntries.TryRemove(id, out var _);
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "UpdateSecondaryList", MethodHookType.Post)]
    public static void OnUpdateSecondaryListPost(ref ulong retval)
    {
        try
        {
            if (CustomSecondaryListEntries.IsEmpty && s_previousCustomPanel == null) return;

            HidePreviousPanel();

            if (CustomSecondaryListEntries.IsEmpty) return;

            var tm = GameSingletonManager.TrainingManager ??
                     throw new NullReferenceException("TrainingManager not found");

            if (tm._ActiveMenuType == app.training.TrainingManager.MenuType.NONE) return;

            var paramData = ((tm._UITrainingMenu as IObject)?.GetField("_ParamData") as ManagedObject)
                            ?.As<app.training.UIFlowTrainingMenu.Param>() ??
                            throw new NullReferenceException(
                                "Could not get field _ParamData of UITrainingMenu");

            var functype = paramData.CurrentData?.FuncType
                           ?? paramData.ViewDataList[paramData.SecondaryListIndex].Data.FuncType;

            if (!FunctionTypeRegistry.TryGetFunctionId((int)functype, out var functionId) ||
                !CustomSecondaryListEntries.TryGetValue(functionId, out var entry))
                return;

            var (inputGuideId, xPos) = entry;

            // we verified that the funcType has a request here, then we check if the elements already exist
            var uiPartsItem = paramData.SecondaryList.GetFocusItem();
            var uiPartsGroup = ManagedProxy<app.UIPartsGroupItem>.Create(uiPartsItem);
            if (uiPartsGroup == null) return;

            var selectItem = uiPartsGroup._SelectItem;
            if (selectItem == null) return;

            const string customPanelName = "p_custom_guide";
            const string customTextName = "e_text_0";

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
                var templateItem = paramData.SecondaryList.GetChild(0)?.GetChild(10);
                if (templateItem == null)
                {
                    API.LogError("template item not found");
                    return;
                }

                var templatePartsGroup = ManagedProxy<app.UIPartsGroupItem>.Create(templateItem);
                var templateSelectItem = templatePartsGroup?._SelectItem;
                if (templateSelectItem == null)
                {
                    API.LogError("template selectItem not found");
                    return;
                }

                via.gui.Panel? sourceInputGuide = null;
                try
                {
                    sourceInputGuide = templateSelectItem.GetGuiChild("p_InputGuide_2_s").As<via.gui.Panel>();
                }
                catch (InvalidOperationException)
                {
                }

                if (sourceInputGuide == null)
                {
                    API.LogError("p_InputGuide_2_s not found");
                    return;
                }

                var duplicatedPanel = sourceInputGuide.duplicate(customPanelName);
                if (duplicatedPanel == null)
                {
                    API.LogError("duplicated panel failed to get created");
                    return;
                }

                customPanel = ManagedProxy<via.gui.Panel>.Create(duplicatedPanel);

                selectItem.addChild(duplicatedPanel);
            }

            via.gui.Text? customText = null;
            try
            {
                customText = customPanel.GetGuiChild(customTextName).As<via.gui.Text>();
            }
            catch (InvalidOperationException)
            {
            }

            if (customText == null)
            {
                API.LogInfo("e_text_0 not found on custom panel");
                return;
            }

            var str = StringPool.GetOrCreate($"<INPT id=\"{inputGuideId.ToString()}\" type=\"d\">");
            (customText as IObject)?.Call("set_Message", str);


            var newPos = via.vec3.REFType.CreateValueType().As<via.vec3>();
            newPos.x = xPos;
            newPos.y = 0;
            newPos.z = 0;
            customText.Position = newPos;

            customPanel.Visible = true;
            customPanel.Priority = 1;
            s_previousCustomPanel = customPanel;

            via.gui.Text? nameText = null;
            foreach (var candidateName in new[] { "e_text_name", "e_txt_name" })
                try
                {
                    nameText = selectItem.GetGuiChild(candidateName).As<via.gui.Text>();
                    if (nameText != null) break;
                }
                catch (InvalidOperationException)
                {
                }

            if (nameText != null)
            {
                s_referenceNameText = nameText;
                s_referenceInitialX = nameText.Position.x;
                s_activeCustomText = customText;
                s_customTextInitialX = xPos;
                s_remainingAnimationFrames = AnimationFrameCount;
            }
        }
        catch (Exception e)
        {
            API.LogError($"Error in UpdateSecondaryListPost({e.Message})");
        }
    }

    [Callback(typeof(LateUpdateBehavior), CallbackType.Post)]
    public static void OnLateUpdateBehavior()
    {
        if (s_remainingAnimationFrames <= 0 || s_referenceNameText == null || s_activeCustomText == null)
            return;

        try
        {
            s_remainingAnimationFrames--;

            var offsetX = s_referenceNameText.Position.x - s_referenceInitialX;

            var newPos = via.vec3.REFType.CreateValueType().As<via.vec3>();
            newPos.x = s_customTextInitialX + offsetX;
            newPos.y = 0;
            newPos.z = 0;
            s_activeCustomText.Position = newPos;

            if (s_remainingAnimationFrames <= 0)
            {
                s_referenceNameText = null;
                s_activeCustomText = null;
            }
        }
        catch (Exception e)
        {
            API.LogError($"Error in SecondaryListHook.OnLateUpdateBehavior({e.Message})");
            s_remainingAnimationFrames = 0;
            s_referenceNameText = null;
            s_activeCustomText = null;
        }
    }

    private static void HidePreviousPanel()
    {
        s_remainingAnimationFrames = 0;
        s_referenceNameText = null;
        s_activeCustomText = null;

        if (s_previousCustomPanel == null) return;

        try
        {
            s_previousCustomPanel.Visible = false;
        }
        catch (Exception)
        {
        }

        s_previousCustomPanel = null;
    }

    internal static void Clear()
    {
        HidePreviousPanel();
        CustomSecondaryListEntries.Clear();
        StringPool.Clear();
    }
}