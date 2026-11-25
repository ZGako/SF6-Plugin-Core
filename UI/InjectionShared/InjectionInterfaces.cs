namespace SF6_Plugin_Core.UI.InjectionShared;

/// <summary>
///     Marker contract for declarative UI tree mutations (reorders, removals, element appends, field overrides, and
///     hook bindings) applied by <c>PauseMenuManager</c> or <c>OptionsSettingsManager</c>.
/// </summary>
public interface IUIModificationRequest
{
    /// <summary>
    ///     Creates an independent, unclaimed copy of this request whose runtime hooks/claims are not yet bound.
    /// </summary>
    /// <returns>A fresh <see cref="IUIModificationRequest" /> with identical configuration.</returns>
    IUIModificationRequest Clone();
}

/// <summary>
///     Base class for modification requests that hold stateful runtime bindings or native objects and therefore
///     cannot be registered at multiple UI paths simultaneously.
/// </summary>
public abstract class ExclusiveModificationRequest : IUIModificationRequest
{
    private bool _isClaimed;

    /// <inheritdoc />
    public abstract IUIModificationRequest Clone();

    /// <summary>
    ///     Marks this request as actively claimed by a UI manager registration; called internally on registration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if this instance is already registered without being unregistered first (use <see cref="Clone" /> to
    ///     reuse the definition elsewhere).
    /// </exception>
    public void Claim()
    {
        if (_isClaimed) throw new InvalidOperationException("This modification request has already been consumed.");

        _isClaimed = true;
    }

    /// <summary>
    ///     Releases the active registration claim so the request can be re-registered after teardown or unregistration.
    /// </summary>
    public void ReleaseClaim()
    {
        _isClaimed = false;
    }
}

/// <summary>
///     Modification request that attaches runtime delegate dispatch to a UI element's allocated function or option
///     type GUID during UI rebuilds.
/// </summary>
public interface IUIHookRequest : IUIModificationRequest
{
    /// <summary>
    ///     Binds this hook's delegate into Core's dispatch tables under <paramref name="functionNameId" /> after the
    ///     element's runtime type integer has been registered.
    /// </summary>
    /// <param name="functionNameId">
    ///     The stable mod or deterministic game <see cref="Guid" /> identifying the target UI
    ///     element.
    /// </param>
    void AddHook(Guid functionNameId);

    /// <summary>
    ///     Detaches this hook's delegate from Core's dispatch tables using the <see cref="Guid" /> captured in
    ///     <see cref="AddHook" />.
    /// </summary>
    void ClearHook();
}

/// <summary>
///     Materializes and owns the native RE Engine UI data objects represented by a
///     <see cref="UICustomRequestNode{TType, TInstantiation}" />.
/// </summary>
/// <typeparam name="TType">
///     The RE Engine UI data proxy type (such as <see cref="app.training.TrainingMenuData" /> or
///     <see cref="app.Option.OptionUnitBase" />).
/// </typeparam>
public interface IUICustomElementInitialization<out TType>
{
    /// <summary>
    ///     Registers <paramref name="functionNameId" /> in the manager's type registry and creates (or updates) the
    ///     globalized RE Engine UI data instance for this node.
    /// </summary>
    /// <param name="functionNameId">The stable mod <see cref="Guid" /> mapped to a runtime function/option type ID.</param>
    /// <returns>The initialized globalized <typeparamref name="TType" /> engine object.</returns>
    TType InitializeCustomElement(Guid functionNameId);

    // message strings handling and stuff. Also Release the managed object created
    /// <summary>
    ///     Disposes all associated <see cref="SF6_Plugin_Core.UI.Localization.CustomMessage" /> registrations and
    ///     releases any globalized RE Engine handles owned by this initializer.
    /// </summary>
    void Clear();

    /// <summary>
    ///     Returns the already-built <typeparamref name="TType" /> engine data object so callers can inspect its runtime
    ///     <c>FuncType</c> or toggle flags such as <c>IsEnabled</c> live.
    /// </summary>
    /// <returns>The built <typeparamref name="TType" /> proxy.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if <see cref="InitializeCustomElement" /> has not run yet or was
    ///     cleared.
    /// </exception>
    public TType GetElementData();
}