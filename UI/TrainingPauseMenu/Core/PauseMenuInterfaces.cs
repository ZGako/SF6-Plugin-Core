namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

public interface IUIModificationRequest
{
    IUIModificationRequest Clone();

}

public interface IUICustomElementInitialization
{
    app.training.TrainingMenuData InitializeCustomElement(Guid functionNameId);

    // message strings handling and stuff. Also Release the managed object created
    void Clear();

    public app.training.TrainingMenuData GetElementData();
}

public interface IUIHookRequest : IUIModificationRequest
{
    void AddHook(Guid functionNameId);

    /// TODO: do I even need this if I clear everything from MenuManager on rebuild
    void ClearHook();
}


public abstract class ExclusiveModificationRequest : IUIModificationRequest
{
    private bool _isClaimed = false;

    public abstract IUIModificationRequest Clone();

    public void Claim()
    {
        if (_isClaimed)
        {
            throw new InvalidOperationException("This modification request has already been consumed.");
        }

        _isClaimed = true;
    }

    public void ReleaseClaim()
    {
        _isClaimed = false;
    }
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

    public static implicit operator TrainingFuncOrGuid(app.training.TrainingFuncType intValue) => new TrainingFuncOrGuid(intValue);
    public static implicit operator TrainingFuncOrGuid(Guid guidValue) => new TrainingFuncOrGuid(guidValue);
}