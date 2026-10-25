using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using Umbra.Common;
using Umbra.QoLBar.UI;

namespace Umbra.QoLBar;

/// <summary>
/// The core service of the QoL Bar plugin. Holds the configuration, executes
/// shortcuts, processes hotkeys and drives the configuration windows.
/// </summary>
[Service]
public sealed class QoLBarManager : IDisposable
{
    public static QoLBarManager Instance { get; private set; }

    /// <summary>
    /// Returns true if a Dalamud plugin with the given internal name is loaded.
    /// </summary>
    public static bool HasPlugin(string name) => Framework.DalamudPlugin.InstalledPlugins.Any(p => p.IsLoaded && p.InternalName == name);

    public QoLBarConfig Config { get; private set; } = new();

    /// <summary>
    /// Incremented whenever the configuration changes. Widgets use this to
    /// detect when their popup needs to be rebuilt.
    /// </summary>
    public int ConfigRevision { get; private set; }

    private readonly FileInfo configFile;

    public QoLBarManager()
    {
        Instance = this;

        // A failure in the constructor of a registered service would break the
        // entire Umbra service container, so everything below is guarded.
        try
        {
            var dir = new DirectoryInfo(Path.Combine(Framework.DalamudPlugin.ConfigDirectory.FullName, "QoLBar"));
            if (!dir.Exists) dir.Create();

            configFile = new FileInfo(Path.Combine(dir.FullName, "config.json"));

            LoadConfig();
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to initialize QoL Bar: {e}");
            EnsureDefaults();
        }

        try
        {
            // Make sure the icon manager is available before any widget is drawn.
            Framework.Service<IconManager>();
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to initialize the QoL Bar icon manager: {e}");
        }
    }

    private void EnsureDefaults()
    {
        Config ??= new QoLBarConfig();
        Config.Settings ??= new QoLBarSettings();
        Config.Bars ??= new Dictionary<string, BarCfg>();
        Config.ConditionSets ??= new List<CndSetCfg>();
        ConfigRevision++;
    }

    #region Configuration Persistence

    private void LoadConfig()
    {
        var firstStart = !configFile.Exists;

        try
        {
            if (!firstStart)
            {
                Config = JsonConvert.DeserializeObject<QoLBarConfig>(File.ReadAllText(configFile.FullName)) ?? new QoLBarConfig();
            }
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to load QoL Bar config: {e.Message}");
            Config = new QoLBarConfig();
        }

        Config.Settings ??= new QoLBarSettings();
        Config.Bars ??= new Dictionary<string, BarCfg>();
        Config.ConditionSets ??= new List<CndSetCfg>();

        // Normalize data that may be missing when importing from older configs.
        foreach (var bar in Config.Bars.Values)
        {
            bar.ShortcutList ??= new List<ShCfg>();
            NormalizeShortcuts(bar.ShortcutList);
        }

        Loc.Load(Config.Settings.Language);

        if (Config.Bars.Count == 0 && firstStart)
        {
            try
            {
                CreateDemoBar();
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to create the QoL Bar demo bar: {e}");
            }
        }

        ConfigRevision++;
    }

    public void SaveConfig()
    {
        if (configFile == null) return;

        try
        {
            File.WriteAllText(configFile.FullName, JsonConvert.SerializeObject(Config, Formatting.Indented));
            ConfigRevision++;
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to save QoL Bar config: {e.Message}");
            PrintError(Loc.Get("Error.SaveFailed"));
        }
    }

    private static void NormalizeShortcuts(List<ShCfg> list)
    {
        if (list == null) return;

        foreach (var sh in list)
        {
            if (sh.Type == ShCfg.ShortcutType.Category)
                sh.SubList ??= new List<ShCfg>();

            if (sh.SubList != null)
                NormalizeShortcuts(sh.SubList);
        }
    }

    private void CreateDemoBar()
    {
        var bar = new BarCfg { Name = Loc.Get("DemoBar.Name") };
        bar.ShortcutList ??= new List<ShCfg>();

        bar.ShortcutList.Add(new ShCfg {
            Name = Loc.Get("DemoBar.Welcome"),
            Command = "/echo " + Loc.Get("DemoBar.WelcomeEcho")
        });

        bar.ShortcutList.Add(new ShCfg {
            Name = Loc.Get("DemoBar.Macro"),
            Command = "//m1"
        });

        var category = new ShCfg {
            Name = Loc.Get("DemoBar.Category"),
            Type = ShCfg.ShortcutType.Category,
            SubList = new List<ShCfg>()
        };
        category.SubList.Add(new ShCfg { Name = Loc.Get("DemoBar.Child1"), Command = "/echo " + Loc.Get("DemoBar.Child1") });
        category.SubList.Add(new ShCfg { Name = Loc.Get("DemoBar.Child2"), Command = "/echo " + Loc.Get("DemoBar.Child2") });
        category.SubList.Add(new ShCfg { Type = ShCfg.ShortcutType.Spacer });
        category.SubList.Add(new ShCfg { Name = Loc.Get("DemoBar.Child3"), Command = "/echo " + Loc.Get("DemoBar.Child3") });
        bar.ShortcutList.Add(category);

        bar.ShortcutList.Add(new ShCfg { Type = ShCfg.ShortcutType.Spacer });

        bar.ShortcutList.Add(new ShCfg {
            Name = Loc.Get("DemoBar.Emote"),
            Command = "/emote wave"
        });

        Config.Bars["demo"] = bar;
        SaveConfig();
    }

    #endregion

    #region Bars

    /// <summary>
    /// Returns the bar with the given ID, creating an empty one if it does
    /// not exist yet.
    /// </summary>
    public BarCfg GetOrCreateBar(string barId)
    {
        if (string.IsNullOrEmpty(barId)) barId = "demo";

        if (!Config.Bars.TryGetValue(barId, out var bar))
        {
            bar = new BarCfg { Name = Loc.Get("Bar.DefaultName") };
            Config.Bars[barId] = bar;
            SaveConfig();
        }

        return bar;
    }

    /// <summary>
    /// Returns the bar with the given ID, or null if it does not exist.
    /// </summary>
    public BarCfg GetBar(string barId)
    {
        return Config.Bars.TryGetValue(barId, out var bar) ? bar : null;
    }

    /// <summary>
    /// Adopts an orphaned bar (a bar no widget is currently bound to) for a
    /// newly created widget instance. Returns the adopted bar ID, or null if
    /// there are no orphaned bars.
    /// </summary>
    public string AdoptOrphanedBar()
    {
        foreach (var (id, _) in Config.Bars)
        {
            if (!_claimedBars.Contains(id)) return id;
        }

        return null;
    }

    private readonly HashSet<string> _claimedBars = new();

    public void ClaimBar(string barId)
    {
        _claimedBars.Add(barId);
    }

    public void ReleaseBar(string barId)
    {
        _claimedBars.Remove(barId);
    }

    public BarCfg CreateBar(string name)
    {
        var id = Guid.NewGuid().ToString();
        var bar = new BarCfg { Name = name };
        Config.Bars[id] = bar;
        SaveConfig();
        return bar;
    }

    public void DeleteBar(string barId)
    {
        if (Config.Bars.Remove(barId))
        {
            SaveConfig();
        }
    }

    public BarCfg DuplicateBar(string barId)
    {
        if (!Config.Bars.TryGetValue(barId, out var source)) return null;

        var json = JsonConvert.SerializeObject(source);
        var copy = JsonConvert.DeserializeObject<BarCfg>(json);
        copy.Name = source.Name + " " + Loc.Get("Bar.CopySuffix");

        var id = Guid.NewGuid().ToString();
        Config.Bars[id] = copy;
        SaveConfig();
        return copy;
    }

    public string ExportBar(string barId)
    {
        if (!Config.Bars.TryGetValue(barId, out var bar)) return null;
        return Importing.ExportBar(bar);
    }

    public string ExportConditionSet(int index)
    {
        if (index < 0 || index >= Config.ConditionSets.Count) return null;
        return Importing.ExportConditionSet(Config.ConditionSets[index]);
    }

    public bool ImportBar(string data)
    {
        var bar = Importing.ImportBar(data);
        if (bar == null) return false;

        bar.ShortcutList ??= new List<ShCfg>();
        NormalizeShortcuts(bar.ShortcutList);

        var id = Guid.NewGuid().ToString();
        Config.Bars[id] = bar;
        SaveConfig();
        return true;
    }

    public bool ImportConditionSet(string data)
    {
        var set = Importing.ImportConditionSet(data);
        if (set == null) return false;

        Config.ConditionSets.Add(set);
        SaveConfig();
        return true;
    }

    #endregion

    #region Execution

    /// <summary>
    /// Executes the given shortcut, honoring its execution mode.
    /// </summary>
    public void ExecuteShortcut(BarCfg bar, ShCfg sh)
    {
        if (sh == null) return;

        switch (sh.Type)
        {
            case ShCfg.ShortcutType.Spacer:
                return;

            case ShCfg.ShortcutType.Command:
            {
                var lines = GetCommandLines(sh.Command);
                if (lines.Count == 0) return;

                switch (sh.Mode)
                {
                    case ShCfg.ShortcutMode.Default:
                        GameActions.QueueCommand(sh.Command);
                        break;
                    case ShCfg.ShortcutMode.Incremental:
                    {
                        var idx = WrapIndex(sh._i, lines.Count);
                        GameActions.QueueCommand(lines[idx]);
                        sh._i = WrapIndex(idx + 1, lines.Count);
                        SaveConfig();
                        break;
                    }
                    case ShCfg.ShortcutMode.Random:
                        GameActions.QueueCommand(lines[Random.Shared.Next(lines.Count)]);
                        break;
                }

                break;
            }

            case ShCfg.ShortcutType.Category:
            {
                var list = sh.SubList ?? new List<ShCfg>();
                if (list.Count == 0) return;

                if (sh.Mode == ShCfg.ShortcutMode.Default)
                {
                    // A category in default mode expands into a menu group.
                    // When executed via hotkey, run its first executable child.
                    var child = FindFirstExecutable(list);
                    if (child != null) ExecuteShortcut(bar, child);
                    return;
                }

                int idx;
                if (sh.Mode == ShCfg.ShortcutMode.Incremental)
                {
                    idx = WrapIndex(sh._i, list.Count);
                    sh._i = WrapIndex(idx + 1, list.Count);
                    SaveConfig();
                }
                else
                {
                    idx = Random.Shared.Next(list.Count);
                }

                ExecuteShortcut(bar, list[idx]);
                break;
            }
        }
    }

    /// <summary>
    /// Executes the first executable shortcut of a bar. Used by bar hotkeys.
    /// </summary>
    public void ExecuteBarDefault(BarCfg bar)
    {
        var sh = FindFirstExecutable(bar.ShortcutList);
        if (sh != null) ExecuteShortcut(bar, sh);
    }

    private static ShCfg FindFirstExecutable(List<ShCfg> list)
    {
        if (list == null) return null;

        foreach (var sh in list)
        {
            switch (sh.Type)
            {
                case ShCfg.ShortcutType.Command:
                    if (!string.IsNullOrWhiteSpace(sh.Command)) return sh;
                    break;
                case ShCfg.ShortcutType.Category:
                    var child = FindFirstExecutable(sh.SubList);
                    if (child != null) return child;
                    break;
            }
        }

        return null;
    }

    private static List<string> GetCommandLines(string command)
    {
        var lines = new List<string>();

        if (string.IsNullOrEmpty(command)) return lines;

        foreach (var line in command.Split('\n'))
        {
            if (!string.IsNullOrEmpty(line))
                lines.Add(line[..Math.Min(line.Length, 180)]);
        }

        return lines;
    }

    private static int WrapIndex(int i, int count)
    {
        if (count <= 0) return 0;
        return ((i % count) + count) % count;
    }

    #endregion

    #region Visibility & Hotkeys

    /// <summary>
    /// Returns true if widgets bound to the given bar should be visible.
    /// </summary>
    public bool IsBarVisible(BarCfg bar)
    {
        if (bar.Hidden) return false;

        if (!Config.Settings.AlwaysDisplayBars && !Framework.Service<IClientState>().IsLoggedIn)
            return false;

        if (bar.ConditionSet >= 0 && bar.ConditionSet < Config.ConditionSets.Count)
            return ConditionManager.CheckConditionSet(Config.ConditionSets[bar.ConditionSet]);

        return true;
    }

    private void RunHotkeys()
    {
        var hotkeys = new List<(BarCfg, ShCfg)>();

        foreach (var bar in Config.Bars.Values)
        {
            if (!IsBarVisible(bar)) continue;
            CollectHotkeys(bar, bar.ShortcutList, hotkeys);
        }

        KeybindManager.DoHotkeys(hotkeys);

        foreach (var bar in Config.Bars.Values)
        {
            if (bar.Hotkey <= 0 || !IsBarVisible(bar)) continue;
            if (KeybindManager.IsHotkeyPressed(bar.Hotkey))
                ExecuteBarDefault(bar);
        }
    }

    private static void CollectHotkeys(BarCfg bar, List<ShCfg> list, List<(BarCfg, ShCfg)> hotkeys)
    {
        if (list == null) return;

        foreach (var sh in list)
        {
            if (sh.Hotkey > 0) hotkeys.Add((bar, sh));
            if (sh.Type == ShCfg.ShortcutType.Category) CollectHotkeys(bar, sh.SubList, hotkeys);
        }
    }

    #endregion

    #region Windows

    public bool IsConfigWindowOpen { get; set; }

    /// <summary>
    /// Opens the configuration window, optionally selecting a bar.
    /// </summary>
    public void ToggleConfigWindow(string selectBarId = null)
    {
        if (IsConfigWindowOpen && (selectBarId == null || selectBarId == QoLBarConfigWindow.SelectedBarId))
        {
            IsConfigWindowOpen = false;
            QoLBarConfigWindow.IsOpen = false;
            return;
        }

        IsConfigWindowOpen = true;
        QoLBarConfigWindow.IsOpen = true;

        if (selectBarId != null)
        {
            QoLBarConfigWindow.SelectedBarId = selectBarId;
        }
        else if (QoLBarConfigWindow.SelectedBarId == null || !Config.Bars.ContainsKey(QoLBarConfigWindow.SelectedBarId))
        {
            QoLBarConfigWindow.SelectedBarId = Config.Bars.Keys.FirstOrDefault();
        }
    }

    #endregion

    #region Framework Hooks

    [WhenFrameworkCompiling(executionOrder: 100)]
    private static void OnFrameworkCompiling()
    {
        // Make sure the Dalamud services are available before initializing.
        Loc.Load("Auto");
        ConditionManager.Initialize();

        try
        {
            GameActions.Initialize();
        }
        catch (Exception e)
        {
            // Signatures may move with every game patch. Command execution
            // (ProcessChatBox) is resolved through the same mechanism, so a
            // failure here is reported but does not stop the plugin.
            Logger.Error($"Failed to initialize QoL Bar game functions: {e.Message}");
        }

        try
        {
            var commandManager = Framework.Service<ICommandManager>();

            commandManager.AddHandler("/qolbar", new CommandInfo(OnQoLBarCommand) {
                HelpMessage = Loc.Get("Cmd.Help.Config"),
                ShowInHelp  = true,
            });
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to register /qolbar command: {e.Message}");
        }
    }

    private static void OnQoLBarCommand(string command, string arguments)
    {
        Instance?.HandleCommand(arguments);
    }

    private void HandleCommand(string arguments)
    {
        var args = (arguments ?? string.Empty).Trim();

        if (args.Length == 0)
        {
            ToggleConfigWindow();
            return;
        }

        var parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var sub = parts[0].ToLowerInvariant();

        switch (sub)
        {
            case "visible":
            case "vis":
            case "v":
            {
                if (parts.Length < 2)
                {
                    PrintError(Loc.Get("Cmd.Error.UsageVisible"));
                    return;
                }

                var reg = System.Text.RegularExpressions.Regex.Match(parts[1], @"^(\w+)\s+(.+)");
                if (!reg.Success)
                {
                    PrintError(Loc.Get("Cmd.Error.UsageVisible"));
                    return;
                }

                var subcommand = reg.Groups[1].Value.ToLowerInvariant();
                var barQuery = reg.Groups[2].Value.Trim();
                var useIndex = int.TryParse(barQuery, out var idx);

                var bars = Config.Bars.Values.ToList();
                var bar = useIndex
                    ? (idx >= 1 && idx <= bars.Count ? bars[idx - 1] : null)
                    : bars.FirstOrDefault(b => b.Name.Equals(barQuery, StringComparison.OrdinalIgnoreCase));

                if (bar == null)
                {
                    PrintError(Loc.F("Cmd.Error.BarNotFound", barQuery));
                    return;
                }

                switch (subcommand)
                {
                    case "on":
                    case "reveal":
                    case "r":
                        bar.Hidden = false;
                        break;
                    case "off":
                    case "hide":
                    case "h":
                        bar.Hidden = true;
                        break;
                    case "toggle":
                    case "t":
                        bar.Hidden = !bar.Hidden;
                        break;
                    default:
                        PrintError(Loc.Get("Cmd.Error.InvalidSubcommand"));
                        return;
                }

                SaveConfig();
                break;
            }

            case "reload":
                LoadConfig();
                PrintEcho(Loc.Get("Cmd.Reloaded"));
                break;

            default:
                PrintError(Loc.F("Cmd.Error.UnknownSubcommand", sub));
                break;
        }
    }

    [OnTick(interval: 16)]
    private void OnTick()
    {
        GameActions.ReadyCommand();
        KeybindManager.UpdateKeyStates();
        RunHotkeys();
        ConditionManager.UpdateCache();
    }

    [OnDraw]
    private void OnDraw()
    {
        DrawSafely("Bar overlay", BarOverlay.Draw);
        DrawSafely("Configuration window", QoLBarConfigWindow.Draw);
        DrawSafely("Shortcut editor", ShortcutEditorWindow.Draw);
        DrawSafely("Icon browser", IconBrowserWindow.Draw);
    }

    // A failure in one window must not take down the whole draw pass, nor
    // propagate into Dalamud's UI builder.
    private static readonly HashSet<string> ReportedDrawErrors = [];

    private static void DrawSafely(string name, Action draw)
    {
        try
        {
            draw();
        }
        catch (Exception e)
        {
            // Failing every frame would spam the log, so only report once.
            if (ReportedDrawErrors.Add(name))
            {
                Logger.Error($"QoL Bar: {name} failed to render: {e}");
            }
        }
    }

    [WhenFrameworkDisposing]
    private void OnFrameworkDisposing()
    {
        SaveConfig();
        GameActions.Dispose();
        IconManager.Instance?.ClearUserIconCache();
    }

    #endregion

    #region Helpers

    public static void PrintEcho(string message)
    {
        Framework.Service<IChatGui>().Print($"[QoL Bar] {message}");
    }

    public static void PrintError(string message)
    {
        Framework.Service<IChatGui>().PrintError($"[QoL Bar] {message}");
    }

    public void Dispose()
    {
        if (Instance == this) Instance = null;
    }

    #endregion
}
