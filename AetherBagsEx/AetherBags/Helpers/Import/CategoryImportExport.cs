using System;
using System.Collections.Generic;
using System.Linq;
using AetherBags.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;

namespace AetherBags.Helpers.Import;

public static class CategoryImportExport
{
    public static void ExportCategoryToClipboard(UserCategoryDefinition category)
    {
        var export = new CategoryExportData
        {
            Version = 1,
            Categories = [category],
        };

        var compressed = Util.SerializeCompressed(export);
        ImGui.SetClipboardText(compressed);

        Services.NotificationManager.AddNotification(
            new Notification { Content = Localization.F("CategoryImportExport.CategoryExported", category.Name), Type = NotificationType.Success }
        );
    }

    public static void ExportAllCategoriesToClipboard(List<UserCategoryDefinition> categories)
    {
        if (categories.Count == 0)
        {
            Services.NotificationManager.AddNotification(
                new Notification { Content = Localization.Get("CategoryImportExport.NoCategoriesToExport"), Type = NotificationType.Warning }
            );
            return;
        }

        var export = new CategoryExportData
        {
            Version = 1,
            Categories = categories.ToList(),
        };

        var compressed = Util.SerializeCompressed(export);
        ImGui.SetClipboardText(compressed);

        Services.NotificationManager.AddNotification(
            new Notification { Content = Localization.F("CategoryImportExport.CategoriesExported", categories.Count), Type = NotificationType.Success }
        );
    }

    public static CategoryExportData? TryParseClipboard()
    {
        string clipboard;
        try
        {
            clipboard = ImGui.GetClipboardText();
        }
        catch
        {
            Services.NotificationManager.AddNotification(
                new Notification { Content = Localization.Get("CategoryImportExport.FailedToReadClipboard"), Type = NotificationType.Error }
            );
            return null;
        }

        if (string.IsNullOrWhiteSpace(clipboard))
        {
            Services.NotificationManager.AddNotification(
                new Notification { Content = Localization.Get("ImportExport.ClipboardEmpty"), Type = NotificationType.Warning }
            );
            return null;
        }

        var data = Util.DeserializeCompressed<CategoryExportData>(clipboard);
        if (data?.Categories is not { Count: > 0 })
        {
            Services.NotificationManager.AddNotification(
                new Notification { Content = Localization.Get("CategoryImportExport.ClipboardNotCategoryData"), Type = NotificationType.Error }
            );
            return null;
        }

        return data;
    }

    public static UserCategoryDefinition? ImportCategoryFromClipboard()
    {
        var data = TryParseClipboard();
        if (data is null) return null;

        if (data.Categories.Count != 1)
        {
            Services.NotificationManager.AddNotification(
                new Notification { Content = Localization.F("CategoryImportExport.ExpectedOneCategory", data.Categories.Count), Type = NotificationType.Warning }
            );
            return null;
        }

        Services.NotificationManager.AddNotification(
            new Notification { Content = Localization.Get("CategoryImportExport.CategoryImported"), Type = NotificationType.Success }
        );

        return data.Categories[0];
    }

    public static bool ImportAllCategoriesFromClipboard(SystemConfiguration config, bool replaceExisting)
    {
        var data = TryParseClipboard();
        if (data is null) return false;

        var dest = config.Categories.UserCategories;

        if (replaceExisting)
        {
            dest.Clear();
            dest.AddRange(data.Categories);
        }
        else
        {
            var byId = dest
                .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                .ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var incoming in data.Categories)
            {
                if (!string.IsNullOrWhiteSpace(incoming.Id) && byId.TryGetValue(incoming.Id, out var existing))
                {
                    existing.Name = incoming.Name;
                    existing.Description = incoming.Description;
                    existing.Order = incoming.Order;
                    existing.Priority = incoming.Priority;
                    existing.Color = incoming.Color;
                    existing.Enabled = incoming.Enabled;
                    existing.Pinned = incoming.Pinned;
                    existing.ItemSortCriteria = CategorySettings.NormalizeItemSortCriteria(incoming.ItemSortCriteria, allowUseGlobal: true);
                    existing.CustomItemOrder = incoming.CustomItemOrder;
                    existing.Rules = incoming.Rules;
                }
                else
                {
                    dest.Add(incoming);
                    if (!string.IsNullOrWhiteSpace(incoming.Id))
                        byId[incoming.Id] = incoming;
                }
            }
        }

        config.Categories.UserCategoriesEnabled = true;
        config.Categories.NormalizeItemSortSettings();
        Util.SaveConfig(config);

        Services.NotificationManager.AddNotification(
            new Notification { Content = Localization.F("CategoryImportExport.CategoriesImported", data.Categories.Count), Type = NotificationType.Success }
        );

        return true;
    }

    public static void ResetAllCategories(SystemConfiguration config)
    {
        config.Categories.UserCategories = new List<UserCategoryDefinition>();
        Util.SaveConfig(config);
    }
}

public class CategoryExportData
{
    public string Format { get; set; } = "AetherBags_Category";
    public int Version { get; set; } = 1;
    public List<UserCategoryDefinition> Categories { get; set; } = new();
}

