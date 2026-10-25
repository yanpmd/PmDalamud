using System;
using System.Linq;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Nodes.Configuration.Layout;
using KamiToolKit.Nodes;

namespace AetherBags.Nodes.Configuration.General;

public sealed class GeneralScrollingAreaNode : ScrollingListNode
{
    public GeneralScrollingAreaNode()
    {
        GeneralSettings config = System.Config.General;

        new ImportExportResetNode().AttachNode(this);

        ItemSpacing = 10;

        AddNode(new FunctionalConfigurationNode());

        AddNode(new LayoutConfigurationNode(RecalculateLayout));

        AddNode(new DtrConfigurationNode());

        AddNode(new CheckboxNode
        {
            Size = new Vector2(300, 20),
            IsVisible = true,
            String = Localization.Get("General.DebugMode"),
            IsChecked = config.DebugEnabled,
            OnClick = isChecked =>
            {
                config.DebugEnabled = isChecked;
            }
        });

        var languages = SystemConfiguration.Languages;
        var languageNames = languages.Select(l => Localization.Get(l.nameKey)).ToList();
        var selectedIndex = Array.FindIndex(languages, l => l.code == config.Language);
        if (selectedIndex < 0) selectedIndex = 0;

        AddNode(new TextNode
        {
            Size = new Vector2(300, 18),
            String = Localization.Get("Settings.Language"),
        });

        var languageDropdown = new TextDropDownNode
        {
            Size = new Vector2(200, 24),
            Options = languageNames,
            MaxListOptions = languages.Length,
            SelectedOption = languageNames[selectedIndex],
        };
        languageDropdown.OnOptionSelected = selected =>
        {
            var index = languageNames.IndexOf(selected);
            if (index < 0 || index >= languages.Length) return;

            config.Language = languages[index].code;
            Util.SaveConfig(System.Config);
            Localization.Load(System.Config.GetLanguageCode());
        };
        AddNode(languageDropdown);
    }
}