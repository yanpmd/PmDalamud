using System;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Inventory;
using AetherBags.Nodes.Input;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace AetherBags.Nodes.Configuration.General;

internal sealed class FunctionalConfigurationNode : TabbedVerticalListNode
{
    private readonly CheckboxNode _hideDefaultBagsCheckboxNode;
    private readonly CheckboxNode _hideSaddlebagsCheckboxNode;
    private readonly CheckboxNode _hideRetainerbagsCheckboxNode;
    private readonly LabeledEnumDropdownNode<InventoryStackMode> _stackDropDown;
    private readonly CheckboxNode _aggregateUnstackableCheckboxNode = null!;

    public FunctionalConfigurationNode()
    {
        GeneralSettings config = System.Config.General;

        ItemVerticalSpacing = 2;

        var titleNode = new CategoryTextNode
        {
            Height = 18,
            String = Localization.Get("Functional.Title"),
        };
        AddNode(titleNode);

        AddTab(1);

        var showWithGameCheckBox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = Localization.Get("Functional.AutoOpenInventory"),
            IsChecked = config.OpenWithGameInventory,
            OnClick = isChecked =>
            {
                config.OpenWithGameInventory = isChecked;
                _hideDefaultBagsCheckboxNode?.IsEnabled = isChecked;
            }
        };
        AddNode(showWithGameCheckBox);

        AddTab(1);
        _hideDefaultBagsCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = Localization.Get("Functional.HideGameInventory"),
            IsEnabled = config.OpenWithGameInventory,
            IsChecked = config.HideGameInventory,
            OnClick = isChecked =>
            {
                config.HideGameInventory = isChecked;
            }
        };
        AddNode(_hideDefaultBagsCheckboxNode);
        SubtractTab(1);

        var showSaddleWithGameCheckBox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = Localization.Get("Functional.AutoOpenSaddleBags"),
            IsChecked = config.OpenSaddleBagsWithGameInventory,
            OnClick = isChecked =>
            {
                config.OpenSaddleBagsWithGameInventory = isChecked;
                _hideSaddlebagsCheckboxNode?.IsEnabled = isChecked;
            }
        };
        AddNode(showSaddleWithGameCheckBox);

        AddTab(1);
        _hideSaddlebagsCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = Localization.Get("Functional.HideGameSaddleBags"),
            IsEnabled = config.OpenSaddleBagsWithGameInventory,
            IsChecked = config.HideGameSaddleBags,
            OnClick = isChecked =>
            {
                config.HideGameSaddleBags = isChecked;
            }
        };
        AddNode(_hideSaddlebagsCheckboxNode);
        SubtractTab(1);

        var showRetainerWithGameCheckBox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = Localization.Get("Functional.AutoOpenRetainer"),
            IsChecked = config.OpenRetainerWithGameInventory,
            OnClick = isChecked =>
            {
                config.OpenRetainerWithGameInventory = isChecked;
                _hideRetainerbagsCheckboxNode?.IsEnabled = isChecked;
            }
        };
        AddNode(showRetainerWithGameCheckBox);

        AddTab(1);
        _hideRetainerbagsCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = Localization.Get("Functional.HideGameRetainer"),
            IsEnabled = config.OpenRetainerWithGameInventory,
            IsChecked = config.HideGameRetainer,
            OnClick = isChecked =>
            {
                config.HideGameRetainer = isChecked;
            }
        };
        AddNode(_hideRetainerbagsCheckboxNode);
        SubtractTab(1);

        var linkItemCheckBox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = Localization.Get("Functional.LinkItems"),
            IsChecked = config.LinkItemEnabled,
            OnClick = isChecked =>
            {
                config.LinkItemEnabled = isChecked;
            }
        };
        AddNode(linkItemCheckBox);

        var animationCheckBox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = Localization.Get("Functional.EnableAnimation"),
            IsChecked = config.AnimationEnabled,
            OnClick = isChecked =>
            {
                config.AnimationEnabled = isChecked;
                InventoryOrchestrator.RefreshHighlights();
            }
        };
        AddNode(animationCheckBox);

        var frameBatchingCheckBox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = Localization.Get("Functional.EnableFrameBatching"),
            TextTooltip = Localization.Get("Functional.EnableFrameBatchingTooltip"),
            IsChecked = config.FrameBatchingEnabled,
            OnClick = isChecked =>
            {
                config.FrameBatchingEnabled = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        AddNode(frameBatchingCheckBox);


        AddNode(new ResNode
        {
            Height = 6
        });

        var searchModeDropDown = new LabeledEnumDropdownNode<SearchMode>
        {
            Size = new Vector2(500, 20),
            LabelText = Localization.Get("Functional.SearchMode"),
            LabelTextFlags = TextFlags.AutoAdjustNodeSize,
            Options = Enum.GetValues<SearchMode>().ToList(),
            SelectedOption = config.SearchMode,
            OnOptionSelected = selected =>
            {
                config.SearchMode = selected;
                InventoryOrchestrator.RefreshAll(updateMaps: false);
            }
        };
        AddNode(searchModeDropDown);

        _stackDropDown = new LabeledEnumDropdownNode<InventoryStackMode>
        {
            Size = new Vector2(500, 20),
            IsEnabled = true,
            LabelText = Localization.Get("Functional.StackMode"),
            LabelTextFlags = TextFlags.AutoAdjustNodeSize,
            Options = Enum.GetValues<InventoryStackMode>().ToList(),
            SelectedOption = config.StackMode,
            OnOptionSelected = selected =>
            {
                config.StackMode = selected;
                _aggregateUnstackableCheckboxNode.IsEnabled = selected == InventoryStackMode.AggregateByItemId;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        AddNode(_stackDropDown);

        AddTab(1);
        _aggregateUnstackableCheckboxNode = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            IsEnabled = config.StackMode == InventoryStackMode.AggregateByItemId,
            String = Localization.Get("Functional.CombineUnstackable"),
            TextTooltip = Localization.Get("Functional.CombineUnstackableTooltip"),
            IsChecked = config.AggregateUnstackableItems,
            OnClick = isChecked =>
            {
                config.AggregateUnstackableItems = isChecked;
                InventoryOrchestrator.RefreshAll(updateMaps: true);
            }
        };
        AddNode(_aggregateUnstackableCheckboxNode);
        SubtractTab(1);
    }
}