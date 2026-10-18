namespace SF6_Plugin_Core.UI.Localization;

/// <summary>
///     Represents a localizable custom message supporting multiple languages.
/// </summary>
public class CustomMessage : IDisposable
{
    // Cache the Engine-allocated ManagedObject strings
    private readonly ManagedObject?[] _cachedEngineStrings = new ManagedObject[sizeof(via.Language)];

    // raw C# strings for each language
    private readonly string[] _translations = new string[sizeof(via.Language)];

    /// <summary>
    ///     Initializes a new instance of the <see cref="CustomMessage" /> class with the specified English fallback text.
    /// </summary>
    /// <param name="englishFallback">The fallback text in English used when other language translations are not provided.</param>
    public CustomMessage(string englishFallback)
    {
        Id = Guid.NewGuid();
        // Fill the array with the fallback text so missing translations don't show up blank
        Array.Fill(_translations, englishFallback);

        MessageRegistry.RegisterCustomMessage(this);
    }

    /// <summary>
    ///     Gets the unique identifier for the custom message.
    /// </summary>
    public Guid Id { get; init; }

    public void Dispose()
    {
        MessageRegistry.UnregisterCustomMessage(Id);
        GC.SuppressFinalize(this);
    }

    internal void EnsureRegistration()
    {
        MessageRegistry.RegisterCustomMessage(this);
    }


    /// <summary>
    ///     Adds or updates a translation for a specific language.
    /// </summary>
    /// <param name="language">The target language.</param>
    /// <param name="text">The translated text.</param>
    /// <returns>The current <see cref="CustomMessage" /> instance for method chaining.</returns>
    public CustomMessage AddTranslation(via.Language language, string text)
    {
        _translations[(int)language] = text;
        return this;
    }


    /// <summary>
    ///     Gets the memory address of the RE Engine-allocated managed string for the current system language,
    ///     lazily initializing it if needed.
    /// </summary>
    /// <remarks> The returned address points to an already globalized RE Engine string. </remarks>
    /// <returns>The native memory address of the engine string.</returns>
    public ulong GetEngineStringAddress()
    {
        var langIndex = (int)app.helper.hGUI.GetSystemLanguage();

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

    /// <summary>
    ///     Clears and releases all cached RE Engine managed strings.
    /// </summary>
    public void ClearCachedEngineStrings()
    {
        for (var i = 0; i < _cachedEngineStrings.Length; i++)
        {
            _cachedEngineStrings[i]?.ReleaseIfGlobalized();
            _cachedEngineStrings[i] = null;
        }
    }
}