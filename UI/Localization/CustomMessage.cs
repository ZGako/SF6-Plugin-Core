namespace SF6_Plugin_Core.UI.Localization;

/// <summary>
///     Represents a UI text message identified by a <see cref="Guid" />, supporting either custom multi-language
///     strings intercepted via <c>app.helper.hLocalize.getMessage</c> or pass-through vanilla SF6 message GUIDs.
/// </summary>
public class CustomMessage : IDisposable
{
    private const int LanguageCount = (int)via.Language.Max;

    // Cache the Engine-allocated ManagedObject strings
    private readonly ManagedObject?[] _cachedEngineStrings = new ManagedObject[LanguageCount];
    private readonly Lock _cacheLock = new();
    private readonly bool _isGameMessage;

    // raw C# strings for each language
    private readonly string[] _translations = new string[LanguageCount];

    /// <summary>
    ///     Creates a new custom message with a generated <see cref="Id" /> and registers it in the localization hook
    ///     registry so engine queries for <see cref="Id" /> return the configured text.
    /// </summary>
    /// <param name="textOrKey">
    ///     Literal fallback string (used across all languages when <paramref name="isKey" /> is <see langword="false" />)
    ///     or a lookup key registered via <see cref="LocalizationManager.LoadTranslations" />.
    /// </param>
    /// <param name="isKey">
    ///     When <see langword="true" />, copies localized strings from <see cref="LocalizationManager" /> (using the
    ///     active <see cref="LocalizationManager.UseFileScope" /> if set, or English/key fallback if missing).
    /// </param>
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

    /// <summary>
    ///     Wraps an existing vanilla SF6 localization GUID without registering any hook override or allocating engine
    ///     strings. <see cref="Dispose" /> is a no-op for instances created with this constructor.
    /// </summary>
    /// <param name="existingGameId">An engine message <see cref="Guid" /> defined in SF6's localization tables.</param>
    public CustomMessage(Guid existingGameId)
    {
        Id = existingGameId;
        _isGameMessage = true;
    }

    /// <summary>
    ///     Gets the <see cref="Guid" /> written into native <c>_MessageID</c>/<c>_GuideMessage</c> fields to reference
    ///     this message during UI rendering.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    ///     Unregisters custom messages from the localization hook table and frees any cached globalized RE Engine
    ///     strings; does nothing when wrapping a vanilla game GUID.
    /// </summary>
    public void Dispose()
    {
        if (_isGameMessage) return;
        MessageRegistry.UnregisterCustomMessage(Id);
    }

    /// <summary>
    ///     Convenience factory equivalent to <c>new CustomMessage(key, isKey: true)</c> for JSON-backed translations.
    /// </summary>
    /// <param name="key">The translation key, optionally prefixed with <c>"FileKey:TranslationKey"</c>.</param>
    /// <returns>A newly registered <see cref="CustomMessage" /> populated from loaded translations.</returns>
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
    ///     Sets or overwrites the text displayed for a specific <see cref="via.Language" /> before engine string
    ///     materialization.
    /// </summary>
    /// <param name="language">The SF6 display language slot to update.</param>
    /// <param name="text">The localized string for <paramref name="language" />.</param>
    /// <returns>The current <see cref="CustomMessage" /> for fluent chaining.</returns>
    public CustomMessage AddTranslation(via.Language language, string text)
    {
        _translations[(int)language] = text;
        return this;
    }

    /// <summary>
    ///     Returns the RE Engine heap address of the cached globalized <c>System.String</c> matching the active
    ///     <c>via.gui.GUISystem.MessageLanguage</c> (falling back to English if out of range), allocating and pinning
    ///     it lazily on first lookup.
    /// </summary>
    /// <returns>The native object address suitable for returning from <c>app.helper.hLocalize.getMessage</c>.</returns>
    public ulong GetEngineStringAddress()
    {
        var langIndex = (int)via.gui.GUISystem.MessageLanguage;
        if (langIndex < 0 || langIndex >= LanguageCount)
            langIndex = (int)via.Language.English;

        lock (_cacheLock)
        {
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
    }

    /// <summary>
    ///     Releases all cached globalized RE Engine <c>System.String</c> instances across every language slot without
    ///     modifying the underlying C# translation array.
    /// </summary>
    public void ClearCachedEngineStrings()
    {
        lock (_cacheLock)
        {
            for (var i = 0; i < _cachedEngineStrings.Length; i++)
            {
                _cachedEngineStrings[i]?.ReleaseIfGlobalized();
                _cachedEngineStrings[i] = null;
            }
        }
    }
}