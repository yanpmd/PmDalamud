using System.IO;
using System.Numerics;
using AetherBags.Addons;
using AetherBags.Helpers.Import;
using AetherBags.Inventory;
using Dalamud.Game.ClientState.Keys;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.Category;

public sealed class CategoryScrollingAreaNode : ScrollingListNode
{
    private AddonCategoryConfigurationWindow? _categoryConfigurationAddon;

    public CategoryScrollingAreaNode()
    {
        InitializeCategoryAddon();

        AddNode(new CategoryGeneralConfigurationNode
        {
            OnLayoutChanged = RefreshLayoutAfterChildChange,
        });

        AddNode(new ResNode{ Height = 10 });

        var categoryButtonRow = new HorizontalListNode
        {
            Size = new Vector2(300, 28),
            ItemSpacing = 4.0f,
        };

        categoryButtonRow.AddNode(new TextButtonNode
        {
            Size = new Vector2(200, 28),
            String = Localization.Get("CategoryConfig.ConfigureCategories"),
            OnClick = () => _categoryConfigurationAddon?.Toggle(),
        });

        categoryButtonRow.AddNode(new ImGuiIconButtonNode
        {
            Width = 28,
            Height = 28,
            TexturePath = Path.Combine(Services.PluginInterface.AssemblyLocation.Directory?.FullName!, @"Assets\Icons\upload.png"),
            TextTooltip = Localization.Get("CategoryConfig.ExportAllTooltip"),
            OnClick = () => CategoryImportExport.ExportAllCategoriesToClipboard(System.Config.Categories.UserCategories),
        });

        categoryButtonRow.AddNode(new ImGuiIconButtonNode
        {
            Width = 28,
            Height = 28,
            TexturePath = Path.Combine(Services.PluginInterface.AssemblyLocation.Directory?.FullName!, @"Assets\Icons\download.png"),
            TextTooltip = Localization.Get("CategoryConfig.ImportAllTooltip"),
            OnClick = HandleBulkImport,
        });

        var resetButton = new HoldButtonNode
        {
            Y = -4,
            Width = 100,
            Height = 28,
            String = Localization.Get("ImportExport.Reset"),
            TextTooltip = Localization.Get("CategoryConfig.ResetAllTooltip"),
            TextNode = { TextColor = ColorHelper.GetColor(50) },
        };

        resetButton.OnClick = () =>
        {
            CheckAndCloseCategoryAddon();

            CategoryImportExport.ResetAllCategories(System.Config);
            InventoryOrchestrator.RefreshAll(updateMaps: true);
            resetButton.Reset();
        };

        categoryButtonRow.AddNode(resetButton);

        AddNode(categoryButtonRow);
    }

    private void RefreshLayoutAfterChildChange()
    {
        RecalculateLayout();
        Services.Framework.RunOnTick(RecalculateLayout, delayTicks: 2);
    }

    private void HandleBulkImport()
    {
        CheckAndCloseCategoryAddon();

        if (!Services.KeyState[VirtualKey.SHIFT]) return;
        CategoryImportExport.ImportAllCategoriesFromClipboard(System.Config, true);
        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }

    private void InitializeCategoryAddon() {
        if (_categoryConfigurationAddon is not null) return;

        _categoryConfigurationAddon = new AddonCategoryConfigurationWindow {
            Size = new Vector2(700.0f, 500.0f),
            InternalName = "AetherBagsCN_CategoryConfig",
            Title = Localization.Get("CategoryConfig.WindowTitle"),
        };
    }

    private void CheckAndCloseCategoryAddon()
    {
        if (_categoryConfigurationAddon != null)
        {
            _categoryConfigurationAddon.Close();
        }
    }

    protected override void Dispose(bool disposing, bool isNativeDestructor)
    {
        if (disposing)
        {
            if (_categoryConfigurationAddon != null)
            {
                _categoryConfigurationAddon.Dispose();
                _categoryConfigurationAddon = null;
            }
        }

        base.Dispose(disposing, isNativeDestructor);
    }
}