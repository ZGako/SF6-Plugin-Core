namespace SF6_Plugin_Core.UI.InjectionShared;

public interface IUIModificationRequest
{
    IUIModificationRequest Clone();
}

public abstract class ExclusiveModificationRequest : IUIModificationRequest
{
    private bool _isClaimed;

    public abstract IUIModificationRequest Clone();

    public void Claim()
    {
        if (_isClaimed) throw new InvalidOperationException("This modification request has already been consumed.");

        _isClaimed = true;
    }

    public void ReleaseClaim()
    {
        _isClaimed = false;
    }
}

public interface IUIHookRequest : IUIModificationRequest
{
    void AddHook(Guid functionNameId);

    /// TODO: do I even need this if I clear everything from MenuManager on rebuild
    void ClearHook();
}

public interface IUICustomElementInitialization<out TType>
{
    TType InitializeCustomElement(Guid functionNameId);

    // message strings handling and stuff. Also Release the managed object created
    void Clear();

    public TType GetElementData();
}