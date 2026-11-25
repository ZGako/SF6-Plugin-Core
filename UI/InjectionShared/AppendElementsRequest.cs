namespace SF6_Plugin_Core.UI.InjectionShared;

/// <summary>
///     Inserts custom <typeparamref name="TNode" /> subtrees relative to existing elements at the target menu node.
/// </summary>
/// <remarks>
///     Uses 1-based attachment slots relative to the <b>original unmodified</b> array:
///     <c>0</c> prepends before index 0 (top of the list), <c>1..originalLength</c> inserts immediately after
///     original element <c>index - 1</c> (which pairs with <see cref="RemoveElementsRequest" /> at <c>index - 1</c>
///     to replace an item in place), and <c>-1</c> appends at the bottom of the list.
/// </remarks>
/// <typeparam name="TNode">Concrete custom request node type.</typeparam>
/// <typeparam name="TType">RE Engine UI element type produced by <typeparamref name="TNode" />.</typeparam>
/// <typeparam name="TInstantiation">Element initializer interface paired with <typeparamref name="TNode" />.</typeparam>
/// <param name="newElements">Pairs of <c>(attachmentSlot, elementNode)</c> defining where each custom element is inserted.</param>
public class AppendElementsRequest<TNode, TType, TInstantiation>(
    List<(int, TNode)> newElements) : ExclusiveModificationRequest
    where TInstantiation : IUICustomElementInitialization<TType>
    where TNode : UICustomRequestNode<TType, TInstantiation>
{
    /// <summary>
    ///     Gets the insertion pairs supplied at construction, evaluated during each UI rebuild.
    /// </summary>
    // sort the new elements by their order index
    protected List<(int, TNode)> NewElements { get; } = newElements;

    /// <summary>
    ///     Builds all appended <typeparamref name="TNode" /> subtrees and groups them into <c>originalArrayLength + 2</c>
    ///     insertion buckets: slot <c>0</c> (top), slots <c>1..originalArrayLength</c> (after each original element),
    ///     and slot <c>originalArrayLength + 1</c> (bottom, resolved from <c>-1</c>).
    /// </summary>
    /// <param name="originalArrayLength">Length of the unmodified target array at the attachment node.</param>
    /// <returns>Array of insertion buckets indexed by target slot; empty buckets remain <see langword="null" />.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when an attachment index falls outside <c>[-1, originalArrayLength]</c> (excluding <c>-1</c> sentinel
    ///     mapping).
    /// </exception>
    public List<TType>[] GetAppendList(int originalArrayLength)
    {
        // Slot 0: Top of page
        // Slots 1..originalArrayLength: Attached after original element (index - 1)
        // Slot originalArrayLength + 1: Bottom of page (when index == -1)
        var appendList = new List<TType>[originalArrayLength + 2];

        foreach (var (index, elementNode) in NewElements)
        {
            var actualIndex = index == -1 ? originalArrayLength + 1 : index;
            if (actualIndex < 0 || actualIndex >= appendList.Length)
                throw new InvalidOperationException("Append index is out of bounds based on given array length.");

            appendList[actualIndex] ??= [];
            appendList[actualIndex].Add(elementNode.Build());
        }

        return appendList;
    }

    /// <summary>
    ///     Detaches runtime hook delegates registered by every appended node and its descendants.
    /// </summary>
    public void ClearHooks()
    {
        foreach (var (_, elementNode) in NewElements) elementNode.ClearHooks();
    }

    /// <summary>
    ///     Disposes custom messages and releases globalized RE Engine objects owned by every appended node and its
    ///     descendants.
    /// </summary>
    public void ReleaseResources()
    {
        foreach (var (_, elementNode) in NewElements) elementNode.ClearNativeResources();
    }

    /// <summary>
    ///     Detaches hooks and immediately releases native/engine resources across all appended node subtrees.
    /// </summary>
    public void Reset()
    {
        ClearHooks();
        ReleaseResources();
    }

    /// <inheritdoc />
    public override AppendElementsRequest<TNode, TType, TInstantiation> Clone()
    {
        var clonedElements = NewElements.Select(e => (e.Item1, e.Item2)).ToList();
        return new AppendElementsRequest<TNode, TType, TInstantiation>(clonedElements);
    }
}