namespace SF6_Plugin_Core.UI.Localization;

//FIXME: refactor
public class CustomMessage
{
    public Guid Id { get; }

    // raw C# strings for each language
    private readonly string[] _translations = new string[sizeof(via.Language)];

    // Cache the Engine-allocated ManagedObject strings
    private readonly ManagedObject?[] _cachedEngineStrings = new ManagedObject[sizeof(via.Language)];

    public CustomMessage(string englishFallback)
    {
        Id = Guid.NewGuid();
        // Fill the array with the fallback text so missing translations don't show up blank
        Array.Fill(_translations, englishFallback);

        MessageRegistry.RegisterCustomMessage(this);
    }

    // Builder pattern method to easily chain translations in your init phase
    public CustomMessage AddTranslation(via.Language language, string text)
    {
        _translations[(int)language] = text;
        return this;
    }

    // Retrieves the raw memory address of the engine string to replace 'retval'
    public ulong GetEngineStringAddress()
    {
        int langIndex = (int)app.helper.hGUI.GetSystemLanguage();

        // Lazy-load: Only allocate in the engine if it's actually requested
        if (_cachedEngineStrings[langIndex] == null)
        {
            // Allocate the string on the RE Engine's managed GC heap
            var engineString = VM.CreateString(_translations[langIndex]);
            engineString.Globalize();

            _cachedEngineStrings[langIndex] = engineString;
        }

        return _cachedEngineStrings[langIndex]!.GetAddress();
    }

    public void ClearCachedEngineStrings()
    {
        for (int i = 0; i < _cachedEngineStrings.Length; i++)
        {
            _cachedEngineStrings[i]?.Release();
            _cachedEngineStrings[i] = null;
        }
    }
}
