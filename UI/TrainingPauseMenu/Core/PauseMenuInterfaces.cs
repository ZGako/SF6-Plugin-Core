namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

public interface IUICustomElementInitialization
{
    app.training.TrainingMenuData InitializeCustomElement(Guid functionNameId);

    // message strings handling and stuff. Also Release the managed object created
    void Clear();

    public app.training.TrainingMenuData GetElementData();
}

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