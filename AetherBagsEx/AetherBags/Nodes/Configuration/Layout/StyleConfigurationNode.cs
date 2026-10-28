using System;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace AetherBags.Nodes.Configuration.Layout;

/// <summary>
/// Style preset picker. Switching a preset changes the window chrome, spacing and
/// item metrics, so the open bag windows are reopened to pick it up.
/// </summary>
internal sealed class StyleConfigurationNode : TabbedVerticalListNode
{
    public StyleConfigurationNode()
    {
        AddNode(new CategoryTextNode
        {
            Height = 18,
            String = Localization.Get("Style.Title"),
        });

        AddTab(1);

        var presets = StylePresets.Items;
        var names = presets.Select(p => Localization.Get(p.nameKey)).ToList();

        int selectedIndex = Array.FindIndex(presets, p => p.preset == StylePresets.GetConfiguredPreset());
        if (selectedIndex < 0) selectedIndex = 0;

        var presetDropdown = new TextDropDownNode
        {
            Size = new Vector2(220, 24),
            Options = names,
            MaxListOptions = names.Count,
            SelectedOption = names[selectedIndex],
        };
        presetDropdown.OnOptionSelected = selected =>
        {
            int index = names.IndexOf(selected);
            if (index < 0 || index >= presets.Length) return;

            StylePresets.Apply(presets[index].preset);
        };
        AddNode(presetDropdown);

        var hintNode = new LabelTextNode
        {
            Size = new Vector2(420, 18),
            String = Localization.Get("Style.Hint"),
            TextTooltip = Localization.Get("Style.HintTooltip"),
        };
        AddNode(hintNode);

        SubtractTab(1);
    }
}
