using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.Localization;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

/// <summary>
///     Selectively overrides metadata and localized message GUID fields on an existing vanilla
///     <see cref="app.training.TrainingMenuData" /> node. Only non-null properties are written during rebuild, and
///     all original fields are restored automatically from the manager's snapshot upon unregistration or teardown.
/// </summary>
/// <remarks>
///     Cannot be registered at the root path <c>[]</c>. Note that <see cref="Clone" /> shares the same
///     <see cref="CustomMessage" /> references across clones.
/// </remarks>
public class FieldOverrideRequest : ExclusiveModificationRequest
{
    /// <summary>Overrides the native widget presentation kind (for example converting <c>SPIN_ITEM</c> to <c>SPIN_ITEM_GUIDE</c>).</summary>
    public app.training.ItemType? Type { get; init; }

    /// <summary>Overrides whether the row or item starts enabled for focus/selection.</summary>
    public bool? IsEnabled { get; init; }

    /// <summary>Overrides whether cursor navigation skips this item.</summary>
    public bool? IsCantFocus { get; init; }

    /// <summary>Overrides SF6's forced-focus behavior on this item.</summary>
    public bool? IsForceFocus { get; init; }

    /// <summary>Overrides whether focusing the item skips triggering focus functions.</summary>
    public bool? IsNotFocusFunc { get; init; }

    /// <summary>Overrides the button used to advance/increment on this item.</summary>
    public app.InputAssign.Digital.Id? NextButton { get; init; }

    /// <summary>Overrides the button used to step back/decrement on this item.</summary>
    public app.InputAssign.Digital.Id? PrevButton { get; init; }

    /// <summary>Overrides the native <c>_MessageID</c> primary label GUID.</summary>
    public CustomMessage? MessageID { get; init; }

    /// <summary>Overrides the native <c>_SubMessageID</c> secondary label GUID.</summary>
    public CustomMessage? SubMessageID { get; init; }

    /// <summary>Overrides the native <c>_GuideMessage</c> footer description GUID.</summary>
    public CustomMessage? GuideMessage { get; init; }

    /// <summary>Overrides the native <c>_GuideMessageID</c> alternate guide message GUID.</summary>
    public CustomMessage? GuideMessageID { get; init; }

    /// <summary>Overrides the inline button prompt icon (for example adding <see cref="app.InputAssign.Digital.Id.UITrainingOptionY" /> to a vanilla item).</summary>
    public app.InputAssign.Digital.Id? GuidIcon { get; init; }

    /// <summary>Overrides the secondary inline button prompt icon (<c>GuidAddIcon</c>).</summary>
    public app.InputAssign.Digital.Id? GuidAddIcon { get; init; }

    /// <summary>Overrides inclusive <c>(Min, Max)</c> bounds mapped to native <c>via.Int2 LimitValue</c>.</summary>
    public (int, int)? LimitValue { get; init; }

    /// <summary>Overrides the primary slider step interval.</summary>
    public int? Interval { get; init; }

    /// <summary>Overrides the secondary/held step interval (<c>_Interval_Option</c>).</summary>
    public int? Interval_Option { get; init; }

    /// <summary>Overrides the native <c>_EndWordsGuid</c> unit/suffix text GUID.</summary>
    public CustomMessage? EndWordsGuid { get; init; }

    /// <summary>Overrides the native <c>SlotID</c> identifier.</summary>
    public int? SlotID { get; init; }

    /// <summary>Overrides the native <see cref="app.training.eVisibleCase" /> visibility filter.</summary>
    public app.training.eVisibleCase? VisibleCase { get; init; }

    internal void ApplyOverrides(ManagedObject elementMo)
    {
        EnsureMessageRegistration();

        var element = elementMo.As<app.training.TrainingMenuData>();

        if (Type.HasValue) element.Type = Type.Value;
        if (IsEnabled.HasValue) element.IsEnabled = IsEnabled.Value;
        if (IsCantFocus.HasValue) element.IsCantFocus = IsCantFocus.Value;
        if (IsForceFocus.HasValue) element.IsForceFocus = IsForceFocus.Value;
        if (IsNotFocusFunc.HasValue) element.IsNotFocusFunc = IsNotFocusFunc.Value;
        if (NextButton.HasValue) element.NextButton = NextButton.Value;
        if (PrevButton.HasValue) element.PrevButton = PrevButton.Value;
        if (GuidIcon.HasValue) element.GuidIcon = GuidIcon.Value;
        if (GuidAddIcon.HasValue) element.GuidAddIcon = GuidAddIcon.Value;
        if (Interval.HasValue) element.Interval = Interval.Value;
        if (Interval_Option.HasValue) element._Interval_Option = Interval_Option.Value;
        if (SlotID.HasValue) element.SlotID = SlotID.Value;
        if (VisibleCase.HasValue) element.VisibleCase = VisibleCase.Value;

        if (MessageID != null) elementMo.SetGuidField("_MessageID", MessageID.Id);
        if (SubMessageID != null) elementMo.SetGuidField("_SubMessageID", SubMessageID.Id);
        if (GuideMessage != null) elementMo.SetGuidField("_GuideMessage", GuideMessage.Id);
        if (GuideMessageID != null) elementMo.SetGuidField("_GuideMessageID", GuideMessageID.Id);
        if (EndWordsGuid != null) elementMo.SetGuidField("_EndWordsGuid", EndWordsGuid.Id);

        if (LimitValue.HasValue)
        {
            var limit = via.Int2.REFType.CreateValueType().As<via.Int2>();
            limit.x = LimitValue.Value.Item1;
            limit.y = LimitValue.Value.Item2;

            element.LimitValue = limit;
        }
    }

    /// <summary>
    ///     Disposes all non-null custom localization messages attached to this override request; called automatically
    ///     by <see cref="Core.PauseMenuManager" /> during deferred disposal after unregistration.
    /// </summary>
    public void Reset()
    {
        MessageID?.Dispose();
        SubMessageID?.Dispose();
        GuideMessage?.Dispose();
        GuideMessageID?.Dispose();
        EndWordsGuid?.Dispose();
    }

    private void EnsureMessageRegistration()
    {
        MessageID?.EnsureRegistration();
        SubMessageID?.EnsureRegistration();
        GuideMessage?.EnsureRegistration();
        GuideMessageID?.EnsureRegistration();
        EndWordsGuid?.EnsureRegistration();
    }

    /// <inheritdoc />
    public override FieldOverrideRequest Clone()
    {
        return new FieldOverrideRequest
        {
            Type = Type,
            IsEnabled = IsEnabled,
            IsCantFocus = IsCantFocus,
            IsForceFocus = IsForceFocus,
            IsNotFocusFunc = IsNotFocusFunc,
            NextButton = NextButton,
            PrevButton = PrevButton,
            MessageID = MessageID,
            SubMessageID = SubMessageID,
            GuideMessage = GuideMessage,
            GuideMessageID = GuideMessageID,
            GuidIcon = GuidIcon,
            GuidAddIcon = GuidAddIcon,
            LimitValue = LimitValue,
            Interval = Interval,
            Interval_Option = Interval_Option,
            EndWordsGuid = EndWordsGuid,
            SlotID = SlotID,
            VisibleCase = VisibleCase
        };
    }
}