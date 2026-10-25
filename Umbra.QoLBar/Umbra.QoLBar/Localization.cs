using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Umbra.Common;

namespace Umbra.QoLBar;

/// <summary>
/// Loads embedded translation files (en/zh-CN) and resolves localized strings.
/// English is always loaded as a fallback.
/// </summary>
public static class Loc
{
    private static readonly Dictionary<string, string> FallbackDict = new();
    private static Dictionary<string, string> Dict = new();

    public static string CurrentLanguage { get; private set; } = "en";

    public static readonly (string code, string nameKey)[] Languages =
    {
        ("Auto", "Lang.Auto"),
        ("en", "Lang.English"),
        ("zh-CN", "Lang.ChineseSimplified"),
    };

    /// <summary>
    /// Loads the translation for the given language code. "Auto" resolves the
    /// language from the Dalamud UI language and OS culture.
    /// </summary>
    public static void Load(string code)
    {
        try
        {
            var fallback = new Dictionary<string, string>();
            LoadInto(fallback, "en");
            FallbackDict.Clear();
            foreach (var (k, v) in fallback) FallbackDict[k] = v;

            var lang = ResolveLanguageCode(code);
            CurrentLanguage = lang;

            if (lang == "en")
            {
                Dict = new Dictionary<string, string>(FallbackDict);
                return;
            }

            var dict = new Dictionary<string, string>(FallbackDict);
            LoadInto(dict, lang);
            Dict = dict;
        }
        catch (Exception e)
        {
            // Never leave the dictionary unusable when localization fails.
            Logger.Error($"Failed to load QoL Bar localization: {e}");
            Dict ??= new Dictionary<string, string>();
        }
    }

    private static void LoadInto(Dictionary<string, string> dict, string lang)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith($"loc.{lang}.json"));

        if (resourceName == null) return;

        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null) return;

        using var reader = new StreamReader(stream);
        var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
        if (loaded == null) return;

        foreach (var (key, value) in loaded) dict[key] = value;
    }

    private static string ResolveLanguageCode(string code)
    {
        if (code is not ("Auto" or null) && Array.Exists(Languages, l => l.code == code))
            return code;

        string uiLanguage = null;

        try
        {
            uiLanguage = Framework.DalamudPlugin?.UiLanguage;
        }
        catch
        {
            // Dalamud may not be fully available yet.
        }

        if (!string.IsNullOrEmpty(uiLanguage))
        {
            if (uiLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return "zh-CN";
            if (Array.Exists(Languages, l => l.code == uiLanguage))
                return uiLanguage;
        }

        if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
            return "zh-CN";

        return "en";
    }

    /// <summary>
    /// Returns the translated string for the given key, or the fallback
    /// (the key itself by default) when no translation exists.
    /// </summary>
    public static string Get(string key, string? fallback = null)
    {
        if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;

        var dict = Dict;
        if (dict == null) return fallback ?? key;

        return dict.TryGetValue(key, out var str) ? str : (fallback ?? key);
    }

    /// <summary>
    /// Returns the translated string for the given key, formatted with the
    /// given arguments.
    /// </summary>
    public static string F(string key, params object?[] args)
    {
        var str = Get(key);
        try
        {
            return string.Format(str, args);
        }
        catch
        {
            return str;
        }
    }
}
