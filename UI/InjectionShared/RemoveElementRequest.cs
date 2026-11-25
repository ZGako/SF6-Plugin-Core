namespace SF6_Plugin_Core.UI.InjectionShared;

/// <summary>
///     Marks existing vanilla elements at the specified 0-based original indices for omission during UI rebuilds.
///     Only non-overlapping <see cref="RemoveElementsRequest" /> instances may coexist on the same menu node; to
///     replace an element in place, pair removal at <c>index</c> with
///     <see cref="AppendElementsRequest{TNode, TType, TInstantiation}" /> at slot <c>index + 1</c>.
/// </summary>
/// <param name="indicesToRemove">The 0-based indices into the original unmodified array to omit.</param>
public class RemoveElementsRequest(List<int> indicesToRemove) : ExclusiveModificationRequest
{
    /// <summary>
    ///     Gets the deduplicated set of 0-based original array indices omitted by this request.
    /// </summary>
    public IReadOnlySet<int> IndicesToRemove { get; } = new HashSet<int>(indicesToRemove);

    /// <inheritdoc />
    public override RemoveElementsRequest Clone()
    {
        return new RemoveElementsRequest([.. IndicesToRemove]);
    }
}