using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

public abstract class CustomElementBase(CustomElementBase.CustomElementInitializer initializer)
    : IUICustomElementInitialization
{
    protected readonly CustomElementInitializer _initializer = initializer;

    public app.training.TrainingMenuData InitializeCustomElement(Guid functionNameId)
    {
        return _initializer.InitializeCustomElement(functionNameId);
    }

    public void Clear()
    {
        _initializer.Clear();
    }

    public app.training.TrainingMenuData GetElementData()
    {
        return _initializer.GetElementData();
    }

    protected class CustomElementInitializer : IUICustomElementInitialization
    {
        // class specific fields
        private ManagedObject? _customElementDataMo;

        // fields of the TrainingMenuData class
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
        public int Interval { get; init; }
        public int Interval_Option { get; init; }
        public CustomMessage? EndWordsGuid { get; init; }
        public int SlotID { get; init; }
        public app.training.eVisibleCase VisibleCase { get; init; } = app.training.eVisibleCase.Visible;

        public app.training.TrainingMenuData InitializeCustomElement(Guid functionNameId)
        {
            // if the MO was already created, we simply return the existing instance with the newly registered functionNameId 
            // (which gets reset each time Build is being called anyways)

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
            // set the non value-type fields
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

            newElement.LimitValue.x = LimitValue.Item1;
            newElement.LimitValue.y = LimitValue.Item2;

            return newElement;
        }

        public app.training.TrainingMenuData GetElementData()
        {
            return _customElementDataMo?.As<app.training.TrainingMenuData>() ??
                   throw new InvalidOperationException("Custom element data has not been initialized.");
        }

        public void Clear()
        {
            MessageID?.Dispose();
            SubMessageID?.Dispose();
            GuideMessage?.Dispose();
            GuideMessageID?.Dispose();
            EndWordsGuid?.Dispose();

            _customElementDataMo?.ReleaseIfGlobalized();
            _customElementDataMo = null;
        }
    }
}