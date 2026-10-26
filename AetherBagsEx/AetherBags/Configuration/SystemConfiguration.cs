using System;

namespace AetherBags.Configuration;

public class SystemConfiguration
{
    public const string FileName = "AetherBagsEx.json";

    public int Version { get; set; } = ConfigMigrator.CurrentVersion;

    private GeneralSettings _general = new();
    private CategorySettings _categories = new();
    private CurrencySettings _currency = new();
    private KeybindSettings _keybinds = new();
    private DtrSettings _dtr = new();

    public GeneralSettings General
    {
        get => _general;
        set => _general = value ?? new();
    }

    public CategorySettings Categories
    {
        get => _categories;
        set => _categories = value ?? new();
    }

    public CurrencySettings Currency
    {
        get => _currency;
        set => _currency = value ?? new();
    }

    public KeybindSettings Keybinds
    {
        get => _keybinds;
        set => _keybinds = value ?? new();
    }

    public DtrSettings Dtr
    {
        get => _dtr;
        set => _dtr = value ?? new();
    }

    /// <summary>
    /// Ensures all nested config objects are initialized. Call after deserialization.
    /// </summary>
    public void EnsureInitialized()
    {
        _general ??= new();
        _categories ??= new();
        _currency ??= new();
        _keybinds ??= new();
        _dtr ??= new();
        Version = ConfigMigrator.CurrentVersion;
        _categories.UserCategories ??= new();
        _categories.NormalizeItemSortSettings();
        _general.InventoryWindowSizing ??= InventoryWindowSizingDefaults.Create(InventoryWindowSizingLimits.Inventory);
        _general.SaddleBagWindowSizing ??= InventoryWindowSizingDefaults.Create(InventoryWindowSizingLimits.SaddleBag);
        _general.RetainerWindowSizing ??= InventoryWindowSizingDefaults.Create(InventoryWindowSizingLimits.Retainer);
        InventoryWindowSizingDefaults.Normalize(_general.InventoryWindowSizing, InventoryWindowSizingLimits.Inventory);
        InventoryWindowSizingDefaults.Normalize(_general.SaddleBagWindowSizing, InventoryWindowSizingLimits.SaddleBag);
        InventoryWindowSizingDefaults.Normalize(_general.RetainerWindowSizing, InventoryWindowSizingLimits.Retainer);
    }

    /// <summary>
    /// Languages offered in the settings menu, mapped to their language code.
    /// The display names are localized through <see cref="Localization"/>.
    /// </summary>
    public static readonly (string code, string nameKey)[] Languages =
    {
        ("Auto", "Lang.Auto"),
        ("en", "Lang.English"),
        ("zh-CN", "Lang.ChineseSimplified"),
    };

    /// <summary>
    /// Resolves the language code to load. Falls back to the launcher's UI
    /// language, then the OS UI culture when set to Auto.
    /// </summary>
    public string GetLanguageCode()
    {
        if (General.Language is not ("Auto" or null) && Array.Exists(Languages, l => l.code == General.Language))
            return General.Language;

        // The launcher UI language is the most reliable signal, as it also
        // covers Chinese Dalamud builds (the international client has no
        // Chinese language, so ClientLanguage can never report it).
        var uiLanguage = Services.PluginInterface.UiLanguage;
        if (!string.IsNullOrEmpty(uiLanguage))
        {
            if (uiLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return "zh-CN";
            if (Array.Exists(Languages, l => l.code == uiLanguage))
                return uiLanguage;
        }

        // Fall back to the OS UI culture for Chinese systems.
        if (global::System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
            return "zh-CN";

        return "en";
    }
}