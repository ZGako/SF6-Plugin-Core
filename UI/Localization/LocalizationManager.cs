using System.Text.Json;

namespace SF6_Plugin_Core.UI.Localization;

public static class LocalizationManager
{
    private const int LanguageCount = (int)via.Language.Max;

    // Maps: FileKey -> (TranslationKey -> string[(int)via.Language.Max])
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string[]>>
        FileTranslations = new(StringComparer.OrdinalIgnoreCase);

    [ThreadStatic] internal static string? CurrentScope;

    /// <summary>
    ///     Temporarily sets the default localization file scope for all CustomMessages created inside the 'using' block.
    /// </summary>
    public static IDisposable UseFileScope(string fileKey)
    {
        var cleanKey = Path.GetFileNameWithoutExtension(fileKey);
        var previousScope = CurrentScope;
        CurrentScope = cleanKey;
        return new ScopeGuard(() => CurrentScope = previousScope);
    }

    /// <summary>
    ///     Loads a JSON file (automatically appending .json if omitted) and registers it under its filename or alias.
    /// </summary>
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