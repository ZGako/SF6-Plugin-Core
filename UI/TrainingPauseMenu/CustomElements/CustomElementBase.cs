using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

public abstract class CustomElementBase : IUICustomTrainingElementInitialization
{
    protected ManagedObject? _customElementDataMo;

    // Fields previously in the nested CustomElementInitializer
    public app.training.ItemType Type { get; init; } = app.training.ItemType.TEXT_ONLY;
    public bool IsEnabled { get; init; } = true;
    public bool IsCantFocus { get; init; } = false;
    public bool IsForceFocus { get; init; } = false;
    public bool IsNotFocusFunc { get; init; } = false;
    public app.InputAssign.Digital.Id NextButton { get; init; } = app.InputAssign.Digital.Id.None;
    public app.InputAssign.Digital.Id PrevButton { get; init; } = app.InputAssign.Digital.Id.None;
    public CustomMessage? MessageID { get; init; }
    public CustomMessage? SubMessageID { get; init; }
    public CustomMessage? GuideMessage { get; init; }
    public CustomMessage? GuideMessageID { get; init; }
    public app.InputAssign.Digital.Id GuidIcon { get; init; } = app.InputAssign.Digital.Id.Invalid;
    public app.InputAssign.Digital.Id GuidAddIcon { get; init; } = app.InputAssign.Digital.Id.Invalid;
    public (int, int) LimitValue { get; init; }
    public int Interval { get; init; } = 1;
    public int Interval_Option { get; init; } = 1;
    public CustomMessage? EndWordsGuid { get; init; }
    public int SlotID { get; init; }
    public app.training.eVisibleCase VisibleCase { get; init; } = app.training.eVisibleCase.Visible;

    public virtual app.training.TrainingMenuData InitializeCustomElement(Guid functionNameId)
    {
        EnsureMessageRegistration();

        if (_customElementDataMo != null)
        {
            var existingElement = _customElementDataMo.As<app.training.TrainingMenuData>();
            existingElement.FuncType =
                (app.training.TrainingFuncType)FunctionTypeRegistry.RegisterNewFunctionType(functionNameId);
            return existingElement;
        }

        var newElementMo = app.training.TrainingMenuData.REFType.CreateInstance(0);
        newElementMo.Globalize();
        _customElementDataMo = newElementMo;

        var newElement = newElementMo.As<app.training.TrainingMenuData>();

        newElement.Type = Type;
        newElement.FuncType =
            (app.training.TrainingFuncType)FunctionTypeRegistry.RegisterNewFunctionType(functionNameId);
        newElement.IsEnabled = IsEnabled;
        newElement.IsCantFocus = IsCantFocus;
        newElement.IsForceFocus = IsForceFocus;
        newElement.IsNotFocusFunc = IsNotFocusFunc;
        newElement.NextButton = NextButton;
        newElement.PrevButton = PrevButton;
        newElement.GuidIcon = GuidIcon;
        newElement.GuidAddIcon = GuidAddIcon;
        newElement.Interval = Interval;
        newElement._Interval_Option = Interval_Option;
        newElement.SlotID = SlotID;
        newElement.VisibleCase = VisibleCase;

        if (MessageID != null) newElementMo.SetGuidField("_MessageID", MessageID.Id);
        if (SubMessageID != null) newElementMo.SetGuidField("_SubMessageID", SubMessageID.Id);
        if (GuideMessage != null) newElementMo.SetGuidField("_GuideMessage", GuideMessage.Id);
        if (GuideMessageID != null) newElementMo.SetGuidField("_GuideMessageID", GuideMessageID.Id);
        if (EndWordsGuid != null) newElementMo.SetGuidField("_EndWordsGuid", EndWordsGuid.Id);

        var limit = via.Int2.REFType.CreateValueType().As<via.Int2>();
        limit.x = LimitValue.Item1;
        limit.y = LimitValue.Item2;

        newElement.LimitValue = limit;

        return newElement;
    }

    public virtual app.training.TrainingMenuData GetElementData()
    {
        return _customElementDataMo?.As<app.training.TrainingMenuData>() ??
               throw new InvalidOperationException("Custom element data has not been initialized.");
    }

    public virtual void Clear()
    {
        MessageID?.Dispose();
        SubMessageID?.Dispose();
        GuideMessage?.Dispose();
        GuideMessageID?.Dispose();
        EndWordsGuid?.Dispose();

        _customElementDataMo?.ReleaseIfGlobalized();
        _customElementDataMo = null;
    }

    protected void EnsureMessageRegistration()
    {
        MessageID?.EnsureRegistration();
        SubMessageID?.EnsureRegistration();
        GuideMessage?.EnsureRegistration();
        GuideMessageID?.EnsureRegistration();
        EndWordsGuid?.EnsureRegistration();
    }
}