namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

//FIXME: refactor

public interface IUIModificationRequest
{
    IUIModificationRequest Clone();

}

public interface IUICustomElementInitialization
{
    app.training.TrainingMenuData InitializeCustomElement(string functionName);

    // message strings handling and stuff. Also Release the managed object created
    void Clear();

    public app.training.TrainingMenuData GetElementData();
}

public interface IUIHookRequest : IUIModificationRequest
{
    void AddDispatcher(string functionName);

    /// TODO: do I even need this if I clear everything from MenuManager on rebuild
    void ClearDispatcher();
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

    public void Release()
    {
        _isClaimed = false;
    }
}