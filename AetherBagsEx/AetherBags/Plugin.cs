using System.Numerics;
using AetherBags.Addons;
using AetherBags.Commands;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Hooks;
using AetherBags.Inventory.Context;
using AetherBags.IPC;
using AetherBags.IPC.AetherBagsAPI;
using AetherBags.Monitoring;
using AetherBags.Nodes.Style;
using Dalamud.Plugin;
using KamiToolKit;

namespace AetherBags;

public class Plugin : IDalamudPlugin
{
    private readonly CommandHandler _commandHandler;
    private readonly InventoryHooks _inventoryHooks;
    private readonly GlobalKeybindHandler _globalKeybindHandler;
    private readonly InventoryMonitor inventoryMonitor;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();

        System.Config = Util.LoadConfigOrDefault();

        Localization.Load(System.Config.GetLanguageCode());

        BackupHelper.DoConfigBackup(pluginInterface);

        KamiToolKitLibrary.Initialize(pluginInterface);

        System.IPC = new IPCService();
        System.IPC.RefreshExternalSources();
        ItemContextMenuHandler.Initialize();
        System.LootedItemsTracker = new LootedItemsTracker();

        System.AddonInventoryWindow = new AddonInventoryWindow
        {
            InternalName = "AetherBagsEx_MainBags",
            Title = Localization.Get("Window.Inventory"),
            Size = new Vector2(750, 750),
            CreateWindowNode = CreateBagWindowNode,
        };

        System.AddonSaddleBagWindow = new AddonSaddleBagWindow
        {
            InternalName = "AetherBagsEx_SaddleBag",
            Title = Localization.Get("Window.SaddleBag"),
            Size = new Vector2(750, 750),
            CreateWindowNode = CreateBagWindowNode,
        };

        System.AddonRetainerWindow = new AddonRetainerWindow
        {
            InternalName = "AetherBagsEx_Retainer",
            Title = Localization.Get("Window.Retainer"),
            Size = new Vector2(750, 750),
            CreateWindowNode = CreateBagWindowNode,
        };

        System.AddonArmouryWindow = new AddonArmouryWindow
        {
            InternalName = "AetherBagsEx_Armoury",
            Title = Localization.Get("Window.Armoury"),
            Size = new Vector2(760, 750),
            CreateWindowNode = CreateBagWindowNode,
        };

        System.AddonConfigurationWindow = new AddonConfigurationWindow
        {
            InternalName = "AetherBagsEx_Config",
            Title = Localization.Get("Window.Config"),
            Size = new Vector2(640, 512),
            CreateWindowNode = CreateStyledWindowNode,
        };

        System.AddonChangelogWindow = new AddonChangelogWindow
        {
            InternalName = "AetherBagsEx_Changelog",
            Title = Localization.Get("Window.Changelog"),
            Size = new Vector2(640, 512),
        };

        Services.PluginInterface.UiBuilder.OpenMainUi += System.AddonInventoryWindow.Toggle;
        Services.PluginInterface.UiBuilder.OpenConfigUi += System.AddonConfigurationWindow.Toggle;

        System.AetherBagsAPI = new AetherBagsIPCProvider();

        _commandHandler = new CommandHandler();

        Services.ClientState.Login += OnLogin;
        Services.ClientState.Logout += OnLogout;

        if (Services.ClientState.IsLoggedIn) {
            Services.Framework.RunOnFrameworkThread(OnLogin);
        }

        _inventoryHooks = new InventoryHooks();
        _globalKeybindHandler = new GlobalKeybindHandler();
        System.DtrService = new DtrService();
        inventoryMonitor = new InventoryMonitor();
    }

    /// <summary>
    /// The window chrome is built while the addon is being allocated, so the
    /// current style is read lazily on every open instead of once at startup.
    /// </summary>
    private static StyledWindowNode CreateStyledWindowNode() => new(StylePresets.Current);

    /// <summary>
    /// Same as <see cref="CreateStyledWindowNode"/> but without the window title,
    /// which only ever repeated the plugin name in the bag windows.
    /// </summary>
    private static StyledWindowNode CreateBagWindowNode() => new(StylePresets.Current) { HideTitle = true };

    public void Dispose()
    {
        // Unsubscribe first: while the plugin is being reloaded, these handlers
        // would otherwise keep firing on addons that get disposed below.
        Services.PluginInterface.UiBuilder.OpenMainUi   -= System.AddonInventoryWindow.Toggle;
        Services.PluginInterface.UiBuilder.OpenConfigUi -= System.AddonConfigurationWindow.Toggle;
        Services.ClientState.Login                     -= OnLogin;
        Services.ClientState.Logout                    -= OnLogout;

        RegexCache.Clear();

        InventoryAddonContextMenu.Close();
        ItemContextMenuHandler.Dispose();
        _inventoryHooks.Dispose();
        _globalKeybindHandler.Dispose();
        System.DtrService.Dispose();
        inventoryMonitor.Dispose();

        System.LootedItemsTracker.Dispose();
        System.AetherBagsAPI?.Dispose();
        System.IPC.Dispose();
        HighlightState.ClearAll();

        System.AddonInventoryWindow.Dispose();
        System.AddonSaddleBagWindow.Dispose();
        System.AddonRetainerWindow.Dispose();
        System.AddonArmouryWindow.Dispose();
        System.AddonConfigurationWindow.Dispose();
        System.AddonChangelogWindow.Dispose();

        Util.SaveConfig(System.Config);
        KamiToolKitLibrary.Dispose();
    }

    private void OnLogin()
    {
        System.Config = Util.LoadConfigOrDefault();
        Localization.Load(System.Config.GetLanguageCode());
        System.IPC.RefreshExternalSources();
        System.LootedItemsTracker.Enable();

        System.AddonInventoryWindow.DebugOpen();
        System.AddonConfigurationWindow.DebugOpen();
    }

    private void OnLogout(int type, int code)
    {
        Util.SaveConfig(System.Config);
        System.LootedItemsTracker.Disable();
        System.AddonInventoryWindow.Close();
        System.AddonSaddleBagWindow.Close();
        System.AddonRetainerWindow.Close();
        System.AddonArmouryWindow.Close();
        System.AddonConfigurationWindow.Close();
        System.AddonChangelogWindow.Close();
    }
}