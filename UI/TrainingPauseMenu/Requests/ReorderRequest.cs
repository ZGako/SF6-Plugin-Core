using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

public class ReorderRequest(List<int> newOrder) : IUIModificationRequest
{
    private List<int> NewOrder { get; init; } = newOrder;

    public int GetMapping(int originalIndex)
    {
        return originalIndex < 0 || originalIndex >= NewOrder.Count
            ? throw new ArgumentOutOfRangeException(nameof(originalIndex), $"Original index {originalIndex} is out of bounds for the new order list.")
            : NewOrder[originalIndex];
    }

    public int GetInverseMapping(int index)
    {
        return index < 0 || index >= NewOrder.Count
            ? throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of bounds for the new order list.")
            : NewOrder.IndexOf(index);
    }

    public IUIModificationRequest Clone()
    {
        return new ReorderRequest([.. NewOrder]);
    }

}