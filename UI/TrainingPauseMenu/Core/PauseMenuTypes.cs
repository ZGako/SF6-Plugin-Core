global using IUICustomTrainingElementInitialization =
    SF6_Plugin_Core.UI.InjectionShared.IUICustomElementInitialization<app.training.TrainingMenuData>;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;


namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

public class
    PauseMenuAppendElementsRequest(
        List<(int, UICustomElementRequestNode)>
            newElements) : AppendElementsRequest<UICustomElementRequestNode, app.training.TrainingMenuData,
    IUICustomTrainingElementInitialization>(newElements);

public readonly struct TrainingFuncOrGuid
{
    internal readonly bool IsGuid;
    internal readonly app.training.TrainingFuncType IntValue;
    internal readonly Guid GuidValue;

    private TrainingFuncOrGuid(app.training.TrainingFuncType intValue)
    {
        IsGuid = false;
        IntValue = intValue;
        GuidValue = Guid.Empty;
    }

    private TrainingFuncOrGuid(Guid guidValue)
    {
        IsGuid = true;
        IntValue = 0;
        GuidValue = guidValue;
    }

    public static implicit operator TrainingFuncOrGuid(app.training.TrainingFuncType intValue)
    {
        return new TrainingFuncOrGuid(intValue);
    }

    public static implicit operator TrainingFuncOrGuid(Guid guidValue)
    {
        return new TrainingFuncOrGuid(guidValue);
    }
}