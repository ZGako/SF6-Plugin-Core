using SF6_Plugin_Core.UI.InjectionShared;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

public class FieldOverrideRequest : ExclusiveModificationRequest
{
    public app.training.ItemType? Type { get; init; }
    public bool? IsEnabled { get; init; }
    public bool? IsCantFocus { get; init; }
    public bool? IsForceFocus { get; init; }
    public bool? IsNotFocusFunc { get; init; }
    public app.InputAssign.Digital.Id? NextButton { get; init; }
    public app.InputAssign.Digital.Id? PrevButton { get; init; }
    public app.InputAssign.Digital.Id? GuidIcon { get; init; }
    public app.InputAssign.Digital.Id? GuidAddIcon { get; init; }
    public (int, int)? LimitValue { get; init; }
    public int? Interval { get; init; }
    public int? Interval_Option { get; init; }
    public int? SlotID { get; init; }
    public app.training.eVisibleCase? VisibleCase { get; init; }

    internal void ApplyOverrides(app.training.TrainingMenuData element)
    {
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

        if (LimitValue.HasValue)
        {
            element.LimitValue.x = LimitValue.Value.Item1;
            element.LimitValue.y = LimitValue.Value.Item2;
        }
    }

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
            GuidIcon = GuidIcon,
            GuidAddIcon = GuidAddIcon,
            LimitValue = LimitValue,
            Interval = Interval,
            Interval_Option = Interval_Option,
            SlotID = SlotID,
            VisibleCase = VisibleCase
        };
    }
}