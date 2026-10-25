using System;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Inventory;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;
using AetherBags.Nodes.Color;

namespace AetherBags.Nodes.Configuration.Layout;

internal class LayoutConfigurationNode : TabbedVerticalListNode
{
    private readonly CompactLookaheadNode _compactLookaheadNode = null!;
    private readonly CheckboxNode _preferLargestFitCheckboxNode = null!;
    private readonly CheckboxNode _useStableInsertCheckboxNode = null!;

    private readonly Action? _onLayoutChanged;

    public LayoutConfigurationNode(Action? onLayoutChanged = null)
    {
        _onLayoutChanged = onLayoutChanged;

        GeneralSettings config = System.Config.General;

        var titleNode = new CategoryTextNode
        {
            Height = 18,
            String = Localization.Get("Layout.Title"),
        };
        AddNode(titleNode);

        AddTab(1);

        AddNode(new WindowSizingConfigurationNode(RefreshLayout));

        AddNode(new ResNode
        {
            Height = 6
        });

        var showRecentlyLootedCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = Localization.Get("Layout.ShowRecentlyLooted"),
            IsChecked = config.ShowRecentlyLooted,
            OnClick = isChecked =>
            {
                config.ShowRecentlyLooted = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        AddNode(showRecentlyLootedCheckboxNode);

        var recentlyLootedColorHandler = new Action<Vector4>(newColor =>
        {
            config.RecentlyLootedHighlightColor = newColor;
            InventoryOrchestrator.RefreshAll(updateMaps: true);
        });

        var recentlyLootedColorNode = new ColorInputRow
        {
            Label = Localization.Get("Layout.HighlightColor"),
            Size = new Vector2(300, 24),
            CurrentColor = config.RecentlyLootedHighlightColor,
            DefaultColor = new GeneralSettings().RecentlyLootedHighlightColor,
            OnColorConfirmed = recentlyLootedColorHandler,
            OnColorChange = recentlyLootedColorHandler,
            OnColorCanceled = recentlyLootedColorHandler,
            OnColorPreviewed = recentlyLootedColorHandler,
            IsEnabled = config.HighlightRecentlyLootedItems
        };

        var highlightRecentlyLootedCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = Localization.Get("Layout.HighlightRecentlyLooted"),
            IsChecked = config.HighlightRecentlyLootedItems,
            OnClick = isChecked =>
            {
                config.HighlightRecentlyLootedItems = isChecked;
                recentlyLootedColorNode.IsEnabled = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        AddNode(highlightRecentlyLootedCheckboxNode);

        AddTab(1);
        AddNode(recentlyLootedColorNode);
        SubtractTab(1);

        var showCategoryItemAmountCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = Localization.Get("Layout.ShowCategoryItemAmount"),
            IsChecked = config.ShowCategoryItemCount,
            OnClick = isChecked =>
            {
                config.ShowCategoryItemCount = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        AddNode(showCategoryItemAmountCheckboxNode);

        var compactPackingCheckboxNode = new CheckboxNode
        {
            Height = 18,
            IsVisible = true,
            String = Localization.Get("Layout.UseCompactPacking"),
            TextTooltip = Localization.Get("Layout.UseCompactPackingTooltip"),
            IsChecked = config.CompactPackingEnabled,
            OnClick = isChecked =>
            {
                config.CompactPackingEnabled = isChecked;
                _preferLargestFitCheckboxNode.IsEnabled = isChecked;
                _useStableInsertCheckboxNode.IsEnabled = isChecked;
                _compactLookaheadNode.CompactLookahead.IsEnabled = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        AddNode(compactPackingCheckboxNode);

        AddTab(1);
        _preferLargestFitCheckboxNode = new CheckboxNode
        {
            Height = 18,
            IsVisible = true,
            String = Localization.Get("Layout.PreferLargestFit"),
            TextTooltip = Localization.Get("Layout.PreferLargestFitTooltip"),
            IsEnabled = config.CompactPackingEnabled,
            IsChecked = config.CompactPreferLargestFit,
            OnClick = isChecked =>
            {
                config.CompactPreferLargestFit = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        AddNode(_preferLargestFitCheckboxNode);

        _useStableInsertCheckboxNode = new CheckboxNode
        {
            Height = 18,
            IsVisible = true,
            String = Localization.Get("Layout.UseStableInsert"),
            TextTooltip = Localization.Get("Layout.UseStableInsertTooltip"),
            IsEnabled = config.CompactPackingEnabled,
            IsChecked = config.CompactStableInsert,
            OnClick = isChecked =>
            {
                config.CompactStableInsert = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        AddNode(_useStableInsertCheckboxNode);

        _compactLookaheadNode = new CompactLookaheadNode
        {
            Size = new Vector2(320, 20)
        };
        AddNode(_compactLookaheadNode);
    }

    private void RefreshLayout()
    {
        RecalculateLayout();
        _onLayoutChanged?.Invoke();
    }
}