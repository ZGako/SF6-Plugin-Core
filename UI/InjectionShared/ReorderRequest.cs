namespace SF6_Plugin_Core.UI.InjectionShared;

/// <summary>
///     Permutes the original elements of a UI menu or spinner array according to an explicit index permutation.
///     At most one <see cref="ReorderRequest" /> may be registered per node, and its length must equal the exact
///     length of the original unmodified child array at that node.
/// </summary>
/// <remarks>
///     Reorder runs before removals and appends during rebuild: <c>newOrder[targetPosition]</c> specifies which
///     original 0-based element occupies <c>targetPosition</c>, and any <see cref="AppendElementsRequest{TNode, TType, TInstantiation}" />
///     items attached after an original index remain anchored immediately after that moved element.
/// </remarks>
/// <param name="newOrder">Permutation of <c>0..originalLength - 1</c> where each value is an original 0-based index.</param>
public class ReorderRequest(List<int> newOrder) : IUIModificationRequest
{
    private List<int> NewOrder { get; } = newOrder;

    /// <inheritdoc />
    public IUIModificationRequest Clone()
    {
        return new ReorderRequest([.. NewOrder]);
    }

    /// <summary>
    ///     Maps a target output slot index (<c>targetPosition</c>) to the original element index that moves there.
    /// </summary>
    /// <param name="originalIndex">The 0-based target loop position (must be within <c>0..NewOrder.Count - 1</c>).</param>
    /// <returns>The original 0-based element index placed at <paramref name="originalIndex" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when the target array length exceeds <see cref="NewOrder" /> (for example, when the node's original
    ///     array size does not match the permutation).
    /// </exception>
    public int GetMapping(int originalIndex)
    {
        return originalIndex < 0 || originalIndex >= NewOrder.Count
            ? throw new ArgumentOutOfRangeException(nameof(originalIndex),
                $"Original index {originalIndex} is out of bounds for the new order list.")
            : NewOrder[originalIndex];
    }

    /// <summary>
    ///     Returns the target slot index at which the specified original element index is placed.
    /// </summary>
    /// <param name="index">The 0-based original element index to locate.</param>
    /// <returns>The reordered position of <paramref name="index" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index" /> is outside <c>0..NewOrder.Count - 1</c>.</exception>
    public int GetInverseMapping(int index)
    {
        return index < 0 || index >= NewOrder.Count
            ? throw new ArgumentOutOfRangeException(nameof(index),
                $"Index {index} is out of bounds for the new order list.")
            : NewOrder.IndexOf(index);
    }
}