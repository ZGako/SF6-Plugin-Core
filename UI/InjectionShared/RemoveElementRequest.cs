namespace SF6_Plugin_Core.UI.InjectionShared;

/// <summary>
///     Represents a request to eliminate (remove) existing UI elements at the specified 0-based original indices.
///     To substitute an existing element at <c>index</c>, pair this with an
///     <see cref="AppendElementsRequest{TNode,TType,TInstantiation}" />
///     targeting <c>index + 1</c>.
/// </summary>
/// <param name="indicesToRemove">The 0-based original indices of the elements to remove.</param>
public class RemoveElementsRequest(List<int> indicesToRemove) : ExclusiveModificationRequest
{
    public IReadOnlySet<int> IndicesToRemove { get; } = new HashSet<int>(indicesToRemove);

    public override RemoveElementsRequest Clone()
    {
        return new RemoveElementsRequest([.. IndicesToRemove]);
    }
}