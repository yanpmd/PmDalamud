using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Tags;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace AetherBags.Nodes.Configuration.General;

internal sealed class DtrConfigurationNode : TabbedVerticalListNode
{
    public DtrConfigurationNode()
    {
        DtrSettings config = System.Config.Dtr;

        ItemVerticalSpacing = 2;

        AddNode(new CategoryTextNode
        {
            Height = 18,
            String = Localization.Get("Dtr.Title"),
        });

        var enabledCheckbox = new CheckboxNode
        {
            Size = Size with { Y = 18 },
            IsVisible = true,
            String = Localization.Get("Dtr.ShowSlotUsage"),
            TextTooltip = Localization.Get("Dtr.ShowSlotUsageTooltip"),
            IsChecked = config.Enabled,
            OnClick = isChecked =>
            {
                config.Enabled = isChecked;
                System.DtrService.UpdateBar();
            }
        };
        AddNode(1, enabledCheckbox);

        AddNode(new ResNode { Height = 6 });

        AddNode(2, new TextNode
        {
            Size = new Vector2(500, 18),
            String = Localization.Get("Dtr.FormatString"),
        });

        var formatInput = new TextInputNode
        {
            Size = new Vector2(500, 28),
            PlaceholderString = DtrSettings.DefaultFormatString,
            String = string.IsNullOrWhiteSpace(config.FormatString) ? DtrSettings.DefaultFormatString : config.FormatString,
            OnInputReceived = input =>
            {
                config.FormatString = input.ExtractText();
                System.DtrService.UpdateBar();
            },
            OnInputComplete = input =>
            {
                config.FormatString = input.ExtractText();
                System.DtrService.UpdateBar();
            },
        };
        AddNode(2, formatInput);

        AddNode(2, new TextNode
        {
            Size = new Vector2(500, 34),
            String = Localization.Get("Dtr.Examples"),
        });

        AddNode(2, new TextNode
        {
            Size = new Vector2(500, 18),
            String = Localization.Get("Dtr.InsertTag"),
        });

        var tagKeys = DtrTagDefinitions.GetKeys();
        var displayNameToKey = tagKeys.ToDictionary(key => Localization.Get($"Dtr.Tag.{key}"), key => key);
        var tagOptions = tagKeys.Select(key => Localization.Get($"Dtr.Tag.{key}")).ToList();

        var tagDropdown = new TextDropDownNode
        {
            Size = new Vector2(320, 24),
            Options = tagOptions,
            MaxListOptions = 8,
            SelectedOption = Localization.Get($"Dtr.Tag.{DtrTagDefinitions.DefaultTagKey}"),
        };
        tagDropdown.OnOptionSelected = selected =>
        {
            if (!displayNameToKey.TryGetValue(selected, out var tagKey)) return;
            if (!DtrTagDefinitions.Templates.TryGetValue(tagKey, out var tag) || string.IsNullOrEmpty(tag)) return;

            var current = formatInput.String.ExtractText();
            if (!string.IsNullOrEmpty(current) && !current.EndsWith(' '))
                current += " ";

            formatInput.String = current + tag;
            config.FormatString = formatInput.String.ExtractText();
            System.DtrService.UpdateBar();
            tagDropdown.SelectedOption = Localization.Get($"Dtr.Tag.{DtrTagDefinitions.DefaultTagKey}");
        };
        AddNode(2, tagDropdown);

        AddNode(2, new TextButtonNode
        {
            Size = new Vector2(120, 28),
            String = Localization.Get("Dtr.ResetFormat"),
            OnClick = () =>
            {
                config.FormatString = DtrSettings.DefaultFormatString;
                formatInput.String = config.FormatString;
                System.DtrService.UpdateBar();
            },
        });
    }
}

