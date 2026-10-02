
namespace SF6_Plugin_Core;

public class EngineStringTracker
{
    // Maps a unique identifier (like the UI component itself) to its currently active engine string
    private readonly Dictionary<ulong, SystemString> _activeStrings = [];

    /// <summary>
    /// Creates an engine string, assigns it via the provided action, and cleans up the old string.
    /// </summary>
    public void AssignText(via.gui.PlayObject uiElementKey, string text, Action<SystemString> fieldAssigner)
    {
        var newEngineString = VM.CreateString(text);

        if (newEngineString == null)
        {
            throw new InvalidOperationException("Failed to create engine string.");
        }

        newEngineString.Globalize();

        try
        {
            fieldAssigner(newEngineString);
        }
        catch (Exception ex)
        {
            newEngineString.Release();
            API.LogError($"Failed to assign engine string to the UI element: {ex.Message}");
            return;
        }

        if (_activeStrings.TryGetValue((uiElementKey as IObject)!.GetAddress(), out var oldString))
        {
            oldString?.Release();
        }

        _activeStrings[(uiElementKey as IObject)!.GetAddress()] = newEngineString;
    }

    /// <summary>
    /// Free all lingering strings when your mod unloads or the UI is destroyed.
    /// </summary>
    public void Clear()
    {
        foreach (var str in _activeStrings.Values)
        {
            str?.Release();
        }
        _activeStrings.Clear();
    }
}