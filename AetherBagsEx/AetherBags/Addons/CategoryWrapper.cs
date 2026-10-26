using System;
using AetherBags.Configuration;
using AetherBags.Inventory.Categories;
using AetherBags.IPC.ExternalCategorySystem;
using Lumina.Excel.Sheets;

namespace AetherBags.Addons;

public enum CategoryWrapperKind { GeneralSettings, User, Override, ExternalSource, GameCategory }

public sealed class CategoryWrapper
{
    public CategoryWrapperKind Kind { get; }
    public UserCategoryDefinition? CategoryDefinition { get; }
    public uint? GameCategoryId { get; }
    public IExternalItemSource? Source { get; }

    private CategoryWrapper(CategoryWrapperKind kind, UserCategoryDefinition? def, uint? gameId, IExternalItemSource? source = null)
    {
        Kind = kind;
        CategoryDefinition = def;
        GameCategoryId = gameId;
        Source = source;
    }

    public static CategoryWrapper ForGeneralSettings()
        => new(CategoryWrapperKind.GeneralSettings, null, null);

    public static CategoryWrapper ForUserOrOverride(UserCategoryDefinition def)
    {
        var kind = def.OverrideSourceKey is null ? CategoryWrapperKind.User : CategoryWrapperKind.Override;
        return new CategoryWrapper(kind, def, null);
    }

    public static CategoryWrapper ForGameCategory(uint id)
        => new(CategoryWrapperKind.GameCategory, null, id);

    public static CategoryWrapper ForExternalSource(IExternalItemSource source)
        => new(CategoryWrapperKind.ExternalSource, null, null, source);

    public string GetLabel() => Kind switch
    {
        CategoryWrapperKind.GeneralSettings => Localization.Get("CategoryWrapper.GeneralSettings"),
        CategoryWrapperKind.GameCategory => ResolveGameCategoryName(GameCategoryId!.Value),
        CategoryWrapperKind.ExternalSource => Source!.DisplayName,
        _ => CategoryDefinition!.Name,
    };

    public string GetSubLabel() => Kind switch
    {
        CategoryWrapperKind.GeneralSettings => Localization.Get("CategoryWrapper.ConfigTag"),
        CategoryWrapperKind.User => UserCategoryMatcher.IsCatchAll(CategoryDefinition!)
            ? Localization.Get("CategoryWrapper.NoValidRules")
            : (CategoryDefinition!.Enabled ? Localization.Get("CategoryWrapper.UserEnabled") : Localization.Get("CategoryWrapper.UserDisabled")),
        CategoryWrapperKind.Override => "Φ " + Localization.F("CategoryWrapper.Overrides", ResolveOverrideSourceName(CategoryDefinition!.OverrideSourceKey!)),
        CategoryWrapperKind.ExternalSource => Source!.IsBuiltIn ? Localization.Get("CategoryWrapper.BuiltInSource") : Localization.Get("CategoryWrapper.ExternalSource"),
        CategoryWrapperKind.GameCategory => Localization.Get("CategoryWrapper.GameCategoryTag"),
        _ => string.Empty,
    };

    public uint? GetId() => null;

    public uint? GetIconId() => 0;

    public int Compare(CategoryWrapper other)
    {
        int kindCmp = ((int)Kind).CompareTo((int)other.Kind);
        if (kindCmp != 0) return kindCmp;

        return Kind switch
        {
            CategoryWrapperKind.GeneralSettings => 0,
            CategoryWrapperKind.GameCategory => GameCategoryId!.Value.CompareTo(other.GameCategoryId!.Value),
            CategoryWrapperKind.ExternalSource => string.Compare(Source!.DisplayName, other.Source!.DisplayName, StringComparison.OrdinalIgnoreCase),
            _ => CategoryDefinition!.Order.CompareTo(other.CategoryDefinition!.Order),
        };
    }

    public static string ResolveGameCategoryName(uint id)
    {
        var sheet = Services.DataManager.GetExcelSheet<ItemUICategory>();
        if (sheet.TryGetRow(id, out var row))
        {
            var name = row.Name.ToString();
            return string.IsNullOrWhiteSpace(name) ? Localization.F("CategoryWrapper.GameCategoryFallback", id) : name;
        }
        return Localization.F("CategoryWrapper.GameCategoryFallback", id);
    }

    public static string ResolveOverrideSourceName(string overrideSourceKey)
    {
        if (CategoryOverrideKey.TryParseGameCategory(overrideSourceKey, out var gameId))
            return ResolveGameCategoryName(gameId);

        return overrideSourceKey;
    }
}
