using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

/// <summary>
///     Abstract base initializer for custom <see cref="app.training.TrainingMenuData" /> nodes in SF6's Training
///     Pause Menu, populating native fields and owning globalized instance and <see cref="CustomMessage" /> lifetimes.
/// </summary>
public abstract class CustomElementBase : IUICustomTrainingElementInitialization
{
    /// <summary>Globalized native <see cref="app.training.TrainingMenuData" /> object reused across rebuilds until cleared.</summary>
    protected ManagedObject? _customElementDataMo;

    // Fields previously in the nested CustomElementInitializer

    /// <summary>Widget presentation kind (<c>TEXT_ONLY</c>, <c>SPIN</c>, <c>SPIN_ITEM</c>, <c>SLIDER</c>, etc.).</summary>
    public app.training.ItemType Type { get; init; } = app.training.ItemType.TEXT_ONLY;

    /// <summary>Whether the row is initially enabled for interaction (can later be mutated live via <see cref="GetElementData" />).</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Prevents cursor focus from landing on this row when <see langword="true" />.</summary>
    public bool IsCantFocus { get; init; } = false;

    /// <summary>Forces cursor focus onto this item according to SF6 menu focus rules.</summary>
    public bool IsForceFocus { get; init; } = false;

    /// <summary>Suppresses invoking this item's function when focus moves without explicit confirmation.</summary>
    public bool IsNotFocusFunc { get; init; } = false;

    /// <summary>Optional input button that increments or steps forward on this item.</summary>
    public app.InputAssign.Digital.Id NextButton { get; init; } = app.InputAssign.Digital.Id.None;

    /// <summary>Optional input button that decrements or steps backward on this item.</summary>
    public app.InputAssign.Digital.Id PrevButton { get; init; } = app.InputAssign.Digital.Id.None;

    /// <summary>Primary row label or spinner option text stored in native <c>_MessageID</c>.</summary>
    public CustomMessage? MessageID { get; init; }

    /// <summary>Secondary label stored in native <c>_SubMessageID</c>, when used by the item layout.</summary>
    public CustomMessage? SubMessageID { get; init; }

    /// <summary>Bottom-bar help description stored in native <c>_GuideMessage</c> (used by <c>UIFlowTrainingMenu</c> footer text).</summary>
    public CustomMessage? GuideMessage { get; init; }

    /// <summary>Alternate guide message GUID stored in native <c>_GuideMessageID</c>.</summary>
    public CustomMessage? GuideMessageID { get; init; }

    /// <summary>Primary button icon shown inline next to the item (for example <see cref="app.InputAssign.Digital.Id.UITrainingOptionY" />).</summary>
    public app.InputAssign.Digital.Id GuidIcon { get; init; } = app.InputAssign.Digital.Id.Invalid;

    /// <summary>Additional inline button icon displayed beside <see cref="GuidIcon" />.</summary>
    public app.InputAssign.Digital.Id GuidAddIcon { get; init; } = app.InputAssign.Digital.Id.Invalid;

    /// <summary>Inclusive <c>(Min, Max)</c> bounds mapped to native <c>via.Int2 LimitValue</c> for sliders and submenu spinners.</summary>
    public (int, int) LimitValue { get; init; }

    /// <summary>Primary step increment applied per directional tick on sliders (defaults to <c>1</c>).</summary>
    public int Interval { get; init; } = 1;

    /// <summary>Secondary/held fast-step increment applied on sliders (written to <c>_Interval_Option</c>, defaults to <c>1</c>).</summary>
    public int Interval_Option { get; init; } = 1;

    /// <summary>Optional suffix/unit text displayed after slider values (written to native <c>_EndWordsGuid</c>, such as <c>"%"</c>).</summary>
    public CustomMessage? EndWordsGuid { get; init; }

    /// <summary>Training save/preset slot identifier stored in native <c>SlotID</c>.</summary>
    public int SlotID { get; init; }

    /// <summary>Visibility rule evaluated by SF6 to show or hide the menu item in specific training sub-modes.</summary>
    public app.training.eVisibleCase VisibleCase { get; init; } = app.training.eVisibleCase.Visible;

    /// <summary>
    ///     Re-registers messages, allocates a new custom <see cref="app.training.TrainingFuncType" /> for
    ///     <paramref name="functionNameId" />, and creates or updates the globalized
    ///     <see cref="app.training.TrainingMenuData" /> object.
    /// </summary>
    /// <param name="functionNameId">Stable mod <see cref="Guid" /> mapped to a newly allocated integer <c>FuncType</c>.</param>
    /// <returns>The initialized globalized <see cref="app.training.TrainingMenuData" /> proxy.</returns>
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

    /// <inheritdoc />
    public virtual app.training.TrainingMenuData GetElementData()
    {
        return _customElementDataMo?.As<app.training.TrainingMenuData>() ??
               throw new InvalidOperationException("Custom element data has not been initialized.");
    }

    /// <inheritdoc />
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

    /// <summary>
    ///     Re-registers all non-null <see cref="CustomMessage" /> fields in the localization registry so reused custom
    ///     nodes recover cleanly across UI rebuilds.
    /// </summary>
    protected void EnsureMessageRegistration()
    {
        MessageID?.EnsureRegistration();
        SubMessageID?.EnsureRegistration();
        GuideMessage?.EnsureRegistration();
        GuideMessageID?.EnsureRegistration();
        EndWordsGuid?.EnsureRegistration();
    }
}