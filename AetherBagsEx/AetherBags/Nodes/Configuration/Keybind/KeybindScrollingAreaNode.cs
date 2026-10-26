using AetherBags.Configuration;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;

namespace AetherBags.Nodes.Configuration.Keybind;

public sealed class KeybindScrollingAreaNode : ScrollingListNode
{
    public KeybindScrollingAreaNode()
    {
        ItemSpacing = 10;

        AddNode(new KeybindConfigurationNode(System.Config.Keybinds));
    }
}

internal sealed class KeybindConfigurationNode : TabbedVerticalListNode
{
    public KeybindConfigurationNode(KeybindSettings config)
    {
        ItemVerticalSpacing = 2;

        AddNode(new CategoryTextNode
        {
            Height = 18,
            String = Localization.Get("Keybind.Title"),
        });

        AddTab(1);

        AddNode(KeybindOptionNode.Create(Localization.Get("Keybind.FocusSearchBar"), config.SearchFocusEnabled, config.SearchFocusKeybind,
            enabled => config.SearchFocusEnabled = enabled,
            kb => config.SearchFocusKeybind = kb,
            showGameConflicts: false));

        AddNode(KeybindOptionNode.Create(Localization.Get("Keybind.OpenBagsFocusSearch"), config.OpenBagsEnabled, config.OpenBagsKeybind,
            enabled => config.OpenBagsEnabled = enabled,
            kb => config.OpenBagsKeybind = kb));

        AddNode(KeybindOptionNode.Create(Localization.Get("Keybind.OpenSaddlebagsFocusSearch"), config.OpenSaddlebagsEnabled, config.OpenSaddlebagsKeybind,
            enabled => config.OpenSaddlebagsEnabled = enabled,
            kb => config.OpenSaddlebagsKeybind = kb));
    }
}
