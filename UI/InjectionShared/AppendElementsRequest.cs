using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

namespace SF6_Plugin_Core.UI.InjectionShared;

/// <summary>
/// </summary>
/// <param name="newElements">
///     This parameter contains the list of new elements to append with the index of the element they'll attach to.
///     <para>FOR THIS SPECIFIC CASE</para>
///     we use indexing starting from 1. So if you put 0, you want the element at the top of the page.
///     -1 is the convention for "add to the end"
/// </param>
public class AppendElementsRequest<TNode, TType, TInstantiation>(
    List<(int, TNode)> newElements) : ExclusiveModificationRequest
    where TInstantiation : IUICustomElementInitialization<TType>
    where TNode : UICustomRequestNode<TType, TInstantiation>
{
    // sort the new elements by their order index
    private List<(int, TNode)> NewElements { get; } = newElements;

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

    public void Reset()
    {
        foreach (var (_, elementNode) in NewElements) elementNode.Clear();
    }

    public override AppendElementsRequest<TNode, TType, TInstantiation> Clone()
    {
        var clonedElements = NewElements.Select(e => (e.Item1, e.Item2)).ToList();
        return new AppendElementsRequest<TNode, TType, TInstantiation>(clonedElements);
    }
}