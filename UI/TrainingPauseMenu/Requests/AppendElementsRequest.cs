

using System.Diagnostics;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using via.gui;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;


/// <summary>
/// 
/// </summary>
/// <param name="newElements">This parameter contains the list of new elements to append with the index of the element they'll attach to.
/// <para>FOR THIS SPECIFIC CASE</para> we use indexing starting from 1. So if you put 0, you want the element at the top of the page. 
/// -1 is the convetion for "add to the end" </param>m
public class AppendElementsRequest(List<(int, UICustomElementNode)> newElements) : ExclusiveModificationRequest
{

    // sort the new elements by their order index
    private List<(int, UICustomElementNode)> NewElements { get; init; } = newElements;

    public List<app.training.TrainingMenuData>[] GetAppendList(int originalArrayLength)
    {
        // Slot 0: Top of page
        // Slots 1..originalArrayLength: Attached after original element (index - 1)
        // Slot originalArrayLength + 1: Bottom of page (when index == -1)
        List<app.training.TrainingMenuData>[] appendList = new List<app.training.TrainingMenuData>[originalArrayLength + 2];

        foreach (var (index, elementNode) in NewElements)
        {
            int actualIndex = index == -1 ? originalArrayLength + 1 : index;
            if (actualIndex < 0 || actualIndex >= appendList.Length)
            {
                throw new InvalidOperationException("Append index is out of bounds based on given array length.");
            }

            appendList[actualIndex] ??= [];
            appendList[actualIndex].Add(elementNode.Build());
        }

        return appendList;
    }

    public void Reset()
    {
        for (int i = 0; i < NewElements.Count; i++)
        {
            var (_, elementNode) = NewElements[i];
            elementNode.Clear();
        }
    }

    public override AppendElementsRequest Clone()
    {
        var clonedElements = NewElements.Select(e => (e.Item1, e.Item2)).ToList();
        return new AppendElementsRequest(clonedElements);
    }
}