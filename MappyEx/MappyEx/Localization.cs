using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using KamiLib.Extensions;

namespace Mappy;

/// <summary>
/// Embedded json based localization.
/// English is always loaded first and acts as the fallback layer, other languages only need to contain the keys they translate.
/// </summary>
public static class Localization
{
    /// <summary>
    /// Languages that can be selected in the configuration window, "Auto" follows the game/dalamud UI language.
    /// </summary>
    public static readonly (string Code, string NameKey)[] SupportedLanguages =
    [
        ("Auto", "Language.Auto"),
        ("en", "Language.English"),
        ("zh-CN", "Language.ChineseSimplified"),
    ];

    private const string FallbackLanguage = "en";

    private static readonly Dictionary<string, string> Strings = new();
    private static readonly SortedSet<string> MissingKeySet = new(StringComparer.Ordinal);

    public static string CurrentLanguage { get; private set; } = FallbackLanguage;

    /// <summary>
    /// Keys that were requested but have no translation in the currently loaded language, useful while writing translations.
    /// </summary>
    public static IReadOnlyCollection<string> MissingKeys => MissingKeySet;

    public static void Load(string language)
    {
        CurrentLanguage = language;
        Strings.Clear();
        MissingKeySet.Clear();

        var fallback = Read(FallbackLanguage) ?? new Dictionary<string, string>();
        var translated = language == FallbackLanguage ? fallback : Read(language) ?? new Dictionary<string, string>();

        foreach (var (key, value) in fallback) Strings[key] = value;
        foreach (var (key, value) in translated) Strings[key] = value;

        // Kamilib ships without translations, let it read from this table, it falls back to the english string at the call site when a key is missing.
        KamiLib.Localization.Resolver = (key, fallbackText) => Get(key, fallbackText);

        // Group display names are localized and cached, they are stale after a language switch.
        Data.MarkerGroups.InvalidateCache();
    }

    public static string Get(string key) => Get(key, null);

    public static string Get(string key, string? fallback)
    {
        if (Strings.TryGetValue(key, out var value)) return value;

        if (MissingKeySet.Count < 512) MissingKeySet.Add(key);

        return fallback ?? key;
    }

    /// <summary>
    /// Get a format string and fill in the placeholders, falls back to the raw string if the format is invalid.
    /// </summary>
    public static string F(string key, params object?[] args)
    {
        var format = Get(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    /// <summary>
    /// Look up Enum.{TypeName}.{Value}, falls back to the [Description] attribute of the enum value.
    /// </summary>
    public static string GetEnum(Enum value)
        => Get($"Enum.{value.GetType().Name}.{value}", value.GetDescription());

    private static Dictionary<string, string>? Read(string language)
    {
        try
        {
            using var stream = FindResource(language);
            if (stream is null) return null;

            using var reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
        }
        catch (Exception exception)
        {
            Service.Log.Error(exception, $"Failed to load localization file loc/{language}.json");
            return null;
        }
    }

    private static Stream? FindResource(string language)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var suffix = $".loc.{language}.json";

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;

            if (assembly.GetManifestResourceStream(resourceName) is { } stream) return stream;
        }

        return null;
    }
}
