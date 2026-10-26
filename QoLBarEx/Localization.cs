using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace QoLBar;

/// <summary>
/// Loads embedded JSON localization files and resolves localized strings.
/// English ("en") is always loaded as the fallback, other languages only need to
/// contain the keys they actually translate. Any missing key falls back to English.
/// </summary>
public static class Localization
{
    private static readonly Dictionary<string, string> strings = new();
    private static readonly SortedSet<string> missingKeys = new();

    /// <summary>The language code currently in use, i.e. "en" or "zh-CN".</summary>
    public static string CurrentLanguage { get; private set; } = "en";

    /// <summary>Keys that were requested but had no translation (shown in the Debug tab).</summary>
    public static IReadOnlyCollection<string> MissingKeys => missingKeys;

    /// <summary>
    /// Loads the given language, falling back to English for anything not translated.
    /// </summary>
    public static void Load(string language)
    {
        CurrentLanguage = language;
        strings.Clear();
        missingKeys.Clear();

        var fallback = Read("en") ?? new Dictionary<string, string>();
        var translated = language == "en" ? fallback : Read(language) ?? new Dictionary<string, string>();

        foreach (var kv in fallback)
            strings[kv.Key] = kv.Value;
        foreach (var kv in translated)
            strings[kv.Key] = kv.Value;
    }

    private static Dictionary<string, string> Read(string language)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream($"QoLBar.loc.{language}.json");
            if (stream == null) return null;

            using var reader = new StreamReader(stream);
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
        }
        catch (Exception e)
        {
            DalamudApi.LogError($"Failed to load localization file for \"{language}\"!", e);
            return null;
        }
    }

    /// <summary>
    /// Gets a localized string, returning the key itself if it is missing.
    /// </summary>
    public static string Get(string key) => Get(key, null);

    /// <summary>
    /// Gets a localized string, falling back to the given value, then the key
    /// itself, if no translation exists.
    /// </summary>
    public static string Get(string key, string fallback)
    {
        if (strings.TryGetValue(key, out var value))
            return value;

        if (missingKeys.Count < 512)
            missingKeys.Add(key);
        return fallback ?? key;
    }

    /// <summary>Gets a localized format string and formats it with the given arguments.</summary>
    public static string F(string key, params object[] args)
    {
        var format = Get(key);
        try { return string.Format(format, args); }
        catch { return format; }
    }
}
