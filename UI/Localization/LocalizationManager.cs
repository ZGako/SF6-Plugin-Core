using System.Text.Json;

namespace SF6_Plugin_Core.UI.Localization;

/// <summary>
///     Loads JSON translation tables keyed by mod alias and resolves per-<see cref="via.Language" /> strings for
///     <see cref="CustomMessage" /> lookups.
/// </summary>
public static class LocalizationManager
{
    private const int LanguageCount = (int)via.Language.Max;

    // Maps: FileKey -> (TranslationKey -> string[(int)via.Language.Max])
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string[]>>
        FileTranslations = new(StringComparer.OrdinalIgnoreCase);

    [ThreadStatic] internal static string? CurrentScope;

    /// <summary>
    ///     Temporarily scopes unqualified <see cref="CustomMessage.FromKey" /> lookups on the current thread to the
    ///     translation table registered under <paramref name="fileKey" />, restoring the previous scope on dispose.
    /// </summary>
    /// <param name="fileKey">The filename (with or without <c>.json</c>) or custom alias passed to <see cref="LoadTranslations" />.</param>
    /// <returns>An <see cref="IDisposable" /> guard that restores the prior thread-local scope when disposed.</returns>
    public static IDisposable UseFileScope(string fileKey)
    {
        var cleanKey = Path.GetFileNameWithoutExtension(fileKey);
        var previousScope = CurrentScope;
        CurrentScope = cleanKey;
        return new ScopeGuard(() => CurrentScope = previousScope);
    }

    /// <summary>
    ///     Loads a JSON translation file mapping translation keys to <see cref="via.Language" /> name/string pairs
    ///     and stores them under <paramref name="customAlias" /> (or the filename without extension). Missing languages
    ///     fall back to the <c>English</c> entry or, if absent, the key name itself.
    /// </summary>
    /// <param name="relativePathWithoutOrWithExt">Path to the localization JSON file (appends <c>.json</c> if omitted).</param>
    /// <param name="customAlias">Optional namespace key used by <see cref="UseFileScope" /> and <c>"Alias:Key"</c> lookups.</param>
    public static void LoadTranslations(string relativePathWithoutOrWithExt, string? customAlias = null)
    {
        var filePath = relativePathWithoutOrWithExt;
        if (!filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) filePath += ".json";

        if (!File.Exists(filePath))
        {
            API.LogWarning($"[Localization] Translation file not found at: {filePath}");
            return;
        }

        var fileKey = customAlias ?? Path.GetFileNameWithoutExtension(filePath);

        try
        {
            var json = File.ReadAllText(filePath);
            var rawData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);

            if (rawData == null) return;

            var fileDict = FileTranslations.GetOrAdd(fileKey, _ => new ConcurrentDictionary<string, string[]>());

            foreach (var (key, langMap) in rawData)
            {
                var langArray = new string[LanguageCount];

                // 1. Find the English fallback (or default to the key name itself) and pre-fill the array
                var fallback = langMap.TryGetValue(nameof(via.Language.English), out var engText) ? engText : key;
                Array.Fill(langArray, fallback);

                // 2. Overwrite specific language slots
                foreach (var (langStr, text) in langMap)
                    if (Enum.TryParse<via.Language>(langStr, true, out var langEnum))
                    {
                        var index = (int)langEnum;
                        if (index >= 0 && index < LanguageCount) langArray[index] = text;
                    }
                    else
                    {
                        API.LogWarning(
                            $"[Localization] Unknown via.Language '{langStr}' for key '{key}' in '{fileKey}'");
                    }

                fileDict[key] = langArray;
            }

            API.LogInfo($"[Localization] Loaded {rawData.Count} keys under file key '{fileKey}'.");
        }
        catch (Exception ex)
        {
            API.LogError($"[Localization] Failed to load translations from {filePath}: {ex.Message}");
        }
    }

    internal static bool TryGetTranslations(string queryKey, out string[]? translations)
    {
        translations = null;

        // Explicit "FileKey:TranslationKey" prefix
        var colonIndex = queryKey.IndexOf(':');
        if (colonIndex > 0)
        {
            var explicitFileKey = queryKey[..colonIndex];
            var actualKey = queryKey[(colonIndex + 1)..];

            if (FileTranslations.TryGetValue(explicitFileKey, out var explicitDict))
                return explicitDict.TryGetValue(actualKey, out translations);
            return false;
        }

        // Scoped lookup via UseFileScope("...")
        if (!string.IsNullOrEmpty(CurrentScope) && FileTranslations.TryGetValue(CurrentScope, out var scopedDict))
            if (scopedDict.TryGetValue(queryKey, out translations))
                return true;

        // Fallback search across all loaded files
        foreach (var dict in FileTranslations.Values)
            if (dict.TryGetValue(queryKey, out translations))
                return true;

        return false;
    }

    private sealed class ScopeGuard(Action onDispose) : IDisposable
    {
        public void Dispose()
        {
            onDispose();
        }
    }
}