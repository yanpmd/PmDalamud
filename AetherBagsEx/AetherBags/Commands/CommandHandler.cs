using System;
using System.Collections.Generic;
using AetherBags.Addons;
using AetherBags.Helpers;
using AetherBags.Inventory;
using AetherBags.Inventory.Items;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace AetherBags.Commands;

public class CommandHandler : IDisposable
{
    private const string MainCommand = "/aetherbagscn";
    private const string ShortCommand = "/abcn";
    private const string HelpDescriptionKey = "Command.HelpDescription";
    private string? _debugVanillaBypassToken;

    public CommandHandler()
    {
        Services.CommandManager.AddHandler(MainCommand, new CommandInfo(OnCommand)
        {
            DisplayOrder = 1,
            ShowInHelp = true,
            HelpMessage = Localization.Get(HelpDescriptionKey)
        });

        Services.CommandManager.AddHandler(ShortCommand, new CommandInfo(OnCommand)
        {
            DisplayOrder = 2,
            ShowInHelp = true,
            HelpMessage = Localization.Get(HelpDescriptionKey)
        });
    }

    private void OnCommand(string command, string args)
    {
        var argsParts = args.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var subCommand = argsParts.Length > 0 ? argsParts[0].ToLowerInvariant() : string.Empty;
        var subArgs = argsParts.Length > 1 ? argsParts[1] : string.Empty;

        switch (subCommand)
        {
            case "":
            case "toggle":
                System.AddonInventoryWindow.Toggle();
                break;

            case "config":
            case "settings":
                System.AddonConfigurationWindow.Toggle();
                break;

            case "show":
            case "open":
                System.AddonInventoryWindow.Open();
                break;

            case "hide":
            case "close":
                System.AddonInventoryWindow.Close();
                break;

            case "search":
                HandleSearch(subArgs);
                break;

            case "import-sk":
                ImportExportResetHelper.TryImportSortaKindaFromClipboard(true);
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "export":
                HandleExport();
                break;

            case "export-categories":
                ImportExportResetHelper.TryExportCategoriesToClipboard();
                break;

            case "import-categories":
                ImportExportResetHelper.TryImportCategoriesFromClipboard(true);
                break;

            case "import":
                ImportExportResetHelper.TryImportConfigFromClipboard();
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "reset":
                ImportExportResetHelper.TryResetConfig();
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "count":
            case "stats":
                PrintInventoryStats();
                break;

            case "debug":
                HandleDebug(subArgs);
                break;

            case "help":
            case "?":
                PrintHelp();
                break;

            case "list":
            case "items":
                ListInventoryItems(subArgs);
                break;

            default:
                PrintChat(Localization.F("Command.UnknownCommand", subCommand));
                break;
        }
    }

    private void HandleDebug(string args)
    {
        if (System.Config?.General?.DebugEnabled != true)
        {
            PrintChat(Localization.Get("Command.DebugDisabled"));
            return;
        }

        var parts = args.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;
        var subArgs = parts.Length > 1 ? parts[1] : string.Empty;

        switch (cmd)
        {
            case "":
            case "help":
                PrintChat(Localization.Get("Command.DebugHelp"));
                break;

            case "saddle":
                System.AddonSaddleBagWindow.Toggle();
                break;

            case "retainer":
                System.AddonRetainerWindow.Toggle();
                break;

            case "changelog":
                System.AddonChangelogWindow.Toggle();
                break;

            case "test":
                HandleTestSource(subArgs);
                break;

            case "batching":
                HandleBatching(subArgs);
                break;

            case "vanilla-bypass":
            case "bypass":
                HandleVanillaInventoryBypass(subArgs);
                break;

            case "refresh":
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                PrintChat(Localization.Get("Command.InventoryRefreshed"));
                break;

            default:
                PrintChat(Localization.F("Command.UnknownDebugCommand", cmd));
                break;
        }
    }

    private void PrintInventoryStats()
    {
        var openWindows = new List<(string Name, IInventoryWindow Window)>();

        if (System.AddonInventoryWindow.IsOpen)
            openWindows.Add((Localization.Get("Command.StatsMain"), System.AddonInventoryWindow));
        if (System.AddonSaddleBagWindow.IsOpen)
            openWindows.Add((Localization.Get("Command.StatsSaddle"), System.AddonSaddleBagWindow));
        if (System.AddonRetainerWindow.IsOpen)
            openWindows.Add((Localization.Get("Command.StatsRetainer"), System.AddonRetainerWindow));

        if (openWindows.Count == 0)
        {
            PrintChat(Localization.Get("Command.NoWindowsOpen"));
            return;
        }

        foreach (var (name, window) in openWindows)
        {
            var stats = window.GetStats();
            PrintChat(Localization.F("Command.StatsLine", name, stats.UsedSlots, stats.TotalSlots, stats.UsagePercent, stats.TotalItems, stats.CategoryCount));
        }

        if (openWindows.Count > 1)
        {
            var combined = new InventoryStats();
            foreach (var (_, window) in openWindows)
            {
                combined += window.GetStats();
            }
            PrintChat(Localization.F("Command.StatsLine", Localization.Get("Command.StatsTotal"), combined.UsedSlots, combined.TotalSlots, combined.UsagePercent, combined.TotalItems, combined.CategoryCount));
        }
    }

    private void HandleSearch(string searchTerm)
    {
        if (!System.AddonInventoryWindow.IsOpen)
        {
            System.AddonInventoryWindow.Open();
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            System.AddonInventoryWindow.SetSearchText(searchTerm);
        }

        PrintChat(Localization.F("Command.SearchingFor", searchTerm));
    }

    private void HandleExport()
    {
        ImportExportResetHelper.TryExportConfigToClipboard(System.Config);
    }

    private void ListInventoryItems(string args)
    {
        if (!System.AddonInventoryWindow.IsOpen)
        {
            PrintChat(Localization.Get("Command.OpenInventoryFirst"));
            return;
        }

        var categories = System.AddonInventoryWindow.GetVisibleCategories();
        if (categories == null || categories.Count == 0)
        {
            PrintChat(Localization.Get("Command.NoItemsFound"));
            return;
        }

        var subCmd = args.Trim().ToLowerInvariant();

        if (subCmd is "ids" or "copy")
        {
            var ids = new List<uint>();
            foreach (var category in categories)
            {
                foreach (var item in category.Items)
                {
                    if (!ids.Contains(item.Item.ItemId))
                        ids.Add(item.Item.ItemId);
                }
            }
            var idsString = string.Join(", ", ids);
            ImGui.SetClipboardText(idsString);
            PrintChat(Localization.F("ContextMenu.CopiedUniqueIds", ids.Count));
            return;
        }

        if (subCmd is "full" or "all")
        {
            var lines = new List<string>();
            foreach (var category in categories)
            {
                foreach (var item in category.Items)
                {
                    lines.Add($"{item.Item.ItemId}: {item.Name} x{item.ItemCount}");
                }
            }
            var fullText = string.Join("\n", lines);
            ImGui.SetClipboardText(fullText);
            PrintChat(Localization.F("Command.CopiedItems", lines.Count));
            return;
        }

        int limit = 20;
        if (!string.IsNullOrWhiteSpace(args) && int.TryParse(args.Trim(), out int parsed))
        {
            limit = parsed;
        }

        PrintChat(Localization.F("Command.ListingUpTo", limit));

        int count = 0;
        foreach (var category in categories)
        {
            foreach (var item in category.Items)
            {
                if (count >= limit) break;
                PrintChat($"  {item.Item.ItemId}: {item.Name} x{item.ItemCount}");
                count++;
            }
            if (count >= limit) break;
        }

        PrintChat(Localization.F("Command.ShownItems", count, categories.Count));
    }

    private void HandleBatching(string args)
    {
        var subCmd = args.Trim().ToLowerInvariant();
        var config = System.Config.General;

        switch (subCmd)
        {
            case "":
            case "toggle":
                config.FrameBatchingEnabled = !config.FrameBatchingEnabled;
                break;

            case "on":
            case "enable":
                config.FrameBatchingEnabled = true;
                break;

            case "off":
            case "disable":
                config.FrameBatchingEnabled = false;
                break;

            case "status":
                PrintChat(Localization.F("Command.BatchingStatus", Localization.Get(config.FrameBatchingEnabled ? "Command.Enabled" : "Command.Disabled")));
                return;

            default:
                PrintChat(Localization.Get("Command.BatchingUsage"));
                return;
        }

        Util.SaveConfig(System.Config);
        PrintChat(Localization.F("Command.BatchingNow", Localization.Get(config.FrameBatchingEnabled ? "Command.Enabled" : "Command.Disabled")));
        InventoryOrchestrator.RefreshAll(updateMaps: true);
    }

    private void HandleVanillaInventoryBypass(string args)
    {
        var api = System.AetherBagsAPI?.API;
        if (api == null)
        {
            PrintChat(Localization.Get("Command.ApiUnavailable"));
            return;
        }

        var parts = args.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var subCmd = parts.Length > 0 ? parts[0].ToLowerInvariant() : "status";
        var subArgs = parts.Length > 1 ? parts[1] : string.Empty;

        switch (subCmd)
        {
            case "":
            case "status":
                PrintChat(api.GetVanillaInventoryBypassStatus());
                break;

            case "on":
            case "acquire":
            case "enable":
                AcquireDebugVanillaBypass(ParseBypassTimeout(subArgs));
                break;

            case "off":
            case "release":
            case "disable":
                ReleaseDebugVanillaBypass();
                break;

            default:
                if (int.TryParse(subCmd, out int timeoutMs))
                {
                    AcquireDebugVanillaBypass(timeoutMs);
                    break;
                }

                PrintChat(Localization.Get("Command.BypassUsage"));
                break;
        }
    }

    private void AcquireDebugVanillaBypass(int timeoutMs)
    {
        var api = System.AetherBagsAPI?.API;
        if (api == null)
        {
            PrintChat(Localization.Get("Command.ApiUnavailable"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(_debugVanillaBypassToken))
            api.ReleaseVanillaInventoryBypass(_debugVanillaBypassToken);

        _debugVanillaBypassToken = api.AcquireVanillaInventoryBypass(Localization.Get("Command.DebugBypassLabel"), timeoutMs);
        PrintChat(Localization.F("Command.BypassAcquired", timeoutMs, _debugVanillaBypassToken));
    }

    private void ReleaseDebugVanillaBypass()
    {
        if (string.IsNullOrWhiteSpace(_debugVanillaBypassToken))
        {
            PrintChat(Localization.Get("Command.NoBypassToken"));
            return;
        }

        bool released = System.AetherBagsAPI?.API.ReleaseVanillaInventoryBypass(_debugVanillaBypassToken) ?? false;
        PrintChat(released ? Localization.Get("Command.BypassReleased") : Localization.Get("Command.BypassExpired"));
        _debugVanillaBypassToken = null;
    }

    private static int ParseBypassTimeout(string args)
    {
        return int.TryParse(args.Trim(), out int timeoutMs) ? timeoutMs : 30_000;
    }

    private void HandleTestSource(string args)
    {
        var testSource = System.IPC?.TestSource;
        if (testSource == null)
        {
            PrintChat(Localization.Get("Command.TestSourceUnavailable"));
            return;
        }

        var subCmd = args.Trim().ToLowerInvariant();

        switch (subCmd)
        {
            case "":
            case "toggle":
                if (testSource.IsReady)
                {
                    testSource.Disable();
                    PrintChat(Localization.Get("Command.TestSourceDisabled"));
                }
                else
                {
                    testSource.Enable();
                    PrintChat(Localization.Get("Command.TestSourceEnabled"));
                }
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "on":
            case "enable":
                testSource.Enable();
                PrintChat(Localization.Get("Command.TestSourceEnabled"));
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "off":
            case "disable":
                testSource.Disable();
                PrintChat(Localization.Get("Command.TestSourceDisabled"));
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "refresh":
                testSource.Refresh();
                PrintChat(Localization.Get("Command.TestSourceRefreshed"));
                InventoryOrchestrator.RefreshAll(updateMaps: true);
                break;

            case "status":
                PrintChat(Localization.F("Command.TestSourceStatus", Localization.Get(testSource.IsReady ? "Command.Enabled" : "Command.Disabled")));
                break;

            default:
                PrintChat(Localization.Get("Command.TestSourceUsage"));
                break;
        }
    }

    private void PrintHelp()
    {
        var commands = new (string Command, string Description)[]
        {
            ("/abcn", "Command.Help.Toggle"),
            ("/abcn config", "Command.Help.Config"),
            ("/abcn show", "Command.Help.Show"),
            ("/abcn hide", "Command.Help.Hide"),

            ("/abcn search <term>", "Command.Help.Search"),

            ("/abcn list", "Command.Help.List"),
            ("/abcn list <n>", "Command.Help.ListN"),
            ("/abcn list ids", "Command.Help.ListIds"),
            ("/abcn list full", "Command.Help.ListFull"),

            ("/abcn stats", "Command.Help.Stats"),

            ("/abcn import", "Command.Help.Import"),
            ("/abcn import-sk", "Command.Help.ImportSk"),
            ("/abcn export", "Command.Help.Export"),
            ("/abcn export-categories", "Command.Help.ExportCategories"),
            ("/abcn import-categories", "Command.Help.ImportCategories"),
            ("/abcn reset", "Command.Help.Reset"),

            ("/abcn help", "Command.Help.Help"),
        };

        var lines = new List<string> { Localization.Get("Command.Help.Header") };

        foreach (var (cmd, desc) in commands)
            lines.Add($"  {cmd}  - {Localization.Get(desc)}");

        lines.Add(System.Config?.General?.DebugEnabled == true
            ? $"  /abcn debug [saddle|retainer|test|refresh]  - {Localization.Get("Command.Help.Debug")}"
            : $"  /abcn debug  - {Localization.Get("Command.Help.DebugDisabled")}");

        PrintChat(string.Join("\n", lines));
    }

    private static void PrintChat(string message)
    {
        Services.ChatGui.Print(message, "AetherBagsEx");
    }

    public void Dispose()
    {
        Services.CommandManager.RemoveHandler(MainCommand);
        Services.CommandManager.RemoveHandler(ShortCommand);
    }
}