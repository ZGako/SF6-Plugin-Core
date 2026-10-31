namespace SF6_Plugin_Core.UI.Localization;

/// <summary>
///     Represents a localizable custom message supporting multiple languages.
/// </summary>
public class CustomMessage : IDisposable
{
    private const int LanguageCount = (int)via.Language.Max;

    // Cache the Engine-allocated ManagedObject strings
    private readonly ManagedObject?[] _cachedEngineStrings = new ManagedObject[LanguageCount];
    private readonly bool _isGameMessage;

    // raw C# strings for each language
    private readonly string[] _translations = new string[LanguageCount];

    /// <summary>
    ///     Initializes a CustomMessage using either raw English fallback text or a localization JSON key.
    /// </summary>
    /// <param name="textOrKey">The English fallback string, or the JSON key if <paramref name="isKey" /> is true.</param>
    /// <param name="isKey">If true, looks up the pre-built translation array in LocalizationManager.</param>
    public CustomMessage(string textOrKey, bool isKey = false)
    {
        Id = Guid.NewGuid();

        if (isKey && LocalizationManager.TryGetTranslations(textOrKey, out var cachedArray) && cachedArray != null)
        {
            // Clone into _translations so per-instance AddTranslation() calls don't mutate the shared cache
            Array.Copy(cachedArray, _translations, LanguageCount);
        }
        else
        {
            if (isKey) API.LogWarning($"[Localization] Key '{textOrKey}' not found. Using key name as fallback.");
            Array.Fill(_translations, textOrKey);
        }

        MessageRegistry.RegisterCustomMessage(this);
    }

    public CustomMessage(Guid existingGameId)
    {
        Id = existingGameId;
        _isGameMessage = true;
    }

    /// <summary>
    ///     Gets the unique identifier for the custom message.
    /// </summary>
    public Guid Id { get; init; }

    public void Dispose()
    {
        if (_isGameMessage) return;
        MessageRegistry.UnregisterCustomMessage(Id);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Optional readable factory alternative to passing 'true' in the constructor.
    /// </summary>
    public static CustomMessage FromKey(string key)
    {
        return new CustomMessage(key, true);
    }

    internal void EnsureRegistration()
    {
        if (_isGameMessage) return;
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
        var langIndex = (int)via.gui.GUISystem.MessageLanguage;

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