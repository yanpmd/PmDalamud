using System;
using System.IO;
using Dalamud.Interface.GameFonts;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Classes;
using KamiLib.CommandManager;
using KamiLib.Window;
using Mappy.Controllers;
using Mappy.Data;
using Mappy.Windows;

namespace Mappy;

public sealed class MappyExPlugin : IDalamudPlugin
{
    public MappyExPlugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        MigrateLegacyConfiguration();

        System.LargeAxisFontHandle = Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle
        {
            SizePt = 72.0f,
            FamilyAndSize = GameFontFamilyAndSize.Axis36,
            Italic = true,
            BaseSkewStrength = 16f,
        });

        System.SystemConfig = SystemConfig.Load();
        Localization.Load(System.SystemConfig.GetLanguageCode());
        System.IconConfig = IconConfig.Load();
        System.FlagConfig = FlagConfig.Load();
        System.CustomMarkerConfig = CustomMarkerConfig.Load();

        // Building the built in catalog reads the Treasure sheet for every entry, do it up front instead of
        // stalling the first frame that draws the map.
        _ = System.CustomMarkerConfig.BuiltInMarkers;

        System.Teleporter = new Teleporter(Service.PluginInterface);

        System.CommandManager = new CommandManager(Service.PluginInterface, "mappyex");

        System.MapRenderer = new MapRenderer.MapRenderer();

        System.ConfigWindow = new ConfigurationWindow();
        System.MapWindow = new MapWindow();

        System.CustomMarkerWindow = new CustomMarkerWindow();

        System.WindowManager = new WindowManager(Service.PluginInterface);
        System.WindowManager.AddWindow(System.ConfigWindow, WindowFlags.IsConfigWindow | WindowFlags.RequireLoggedIn);
        System.WindowManager.AddWindow(System.MapWindow, WindowFlags.RequireLoggedIn);
        System.WindowManager.AddWindow(System.CustomMarkerWindow, WindowFlags.None);

        System.FlagController = new FlagController();
        System.AreaMapController = new AddonAreaMapController();
        System.IntegrationsController = new IntegrationsController();

        Service.Framework.Update += OnFrameworkUpdate;

        Service.PluginInterface.UiBuilder.OpenMainUi += OpenMapWindow;

        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            BaseActivationPath = "/fatelist",
            EnableDelegate = _ => System.WindowManager.OpenOrCreateUnique<FateListWindow>(WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn),
            DisableDelegate = _ => System.WindowManager.GetWindow<FateListWindow>()?.Close(),
            ToggleDelegate = _ => System.WindowManager.GetWindow<FateListWindow>()?.UnCollapseOrToggle(),
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            BaseActivationPath = "/questlist",
            EnableDelegate = _ => System.WindowManager.OpenOrCreateUnique<QuestListWindow>(WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn),
            DisableDelegate = _ => System.WindowManager.GetWindow<QuestListWindow>()?.Close(),
            ToggleDelegate = _ => System.WindowManager.GetWindow<QuestListWindow>()?.UnCollapseOrToggle(),
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            BaseActivationPath = "/flaglist",
            EnableDelegate = _ => System.WindowManager.OpenOrCreateUnique<FlagHistoryWindow>(WindowFlags.OpenImmediately | WindowFlags.RequireLoggedIn),
            DisableDelegate = _ => System.WindowManager.GetWindow<FlagHistoryWindow>()?.Close(),
            ToggleDelegate = _ => System.WindowManager.GetWindow<FlagHistoryWindow>()?.UnCollapseOrToggle(),
        });
    }

    private unsafe void OpenMapWindow() => AgentMap.Instance()->Show();

    private static void OnFrameworkUpdate(IFramework framework) => AethernetNavigator.Update();

    /// <summary>
    /// The plugin was renamed from Mappy to MappyEx, and the configuration directory follows the internal name.
    /// Copy the old files over once so an existing install does not lose its settings after the rename.
    /// </summary>
    private static void MigrateLegacyConfiguration()
    {
        try
        {
            var currentDirectory = Service.PluginInterface.ConfigDirectory.FullName;

            // Nothing to do when this install already has its own configuration.
            if (Directory.Exists(currentDirectory) && Directory.GetFiles(currentDirectory).Length > 0) return;

            var parentDirectory = Path.GetDirectoryName(currentDirectory);
            if (parentDirectory is null) return;

            var legacyDirectory = Path.Combine(parentDirectory, "Mappy");
            if (!Directory.Exists(legacyDirectory)) return;

            Directory.CreateDirectory(currentDirectory);

            foreach (var file in Directory.GetFiles(legacyDirectory))
            {
                var destination = Path.Combine(currentDirectory, Path.GetFileName(file));
                if (File.Exists(destination)) continue;

                File.Copy(file, destination);
            }

            Service.Log.Information($"Migrated configuration from {legacyDirectory} to {currentDirectory}");
        }
        catch (Exception exception)
        {
            Service.Log.Warning(exception, "Failed to migrate the legacy Mappy configuration");
        }
    }

    public void Dispose()
    {
        Service.Framework.Update -= OnFrameworkUpdate;

        AethernetNavigator.Cancel();

        System.MapWindow.OnClose();
        System.WindowManager.Dispose();
        System.IntegrationsController.Dispose();
        System.AreaMapController.Dispose();
        System.FlagController.Dispose();
        System.MapRenderer.Dispose();

        Service.PluginInterface.UiBuilder.OpenMainUi -= OpenMapWindow;
    }
}