using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Umbra.MoreControls.Config;

/// <summary>
/// Owns the plugin's own configuration file. Umbra's <c>ConfigManager</c> only
/// collects <c>[ConfigVariable]</c> members before custom plugins are loaded, so
/// plugins have to persist their own JSON document instead.
///
/// Uses <see cref="System.Text.Json"/> on purpose: it ships with the runtime,
/// while assemblies like Newtonsoft.Json are neither in Umbra's plugin load
/// context whitelist nor resolvable from the plugin directory.
/// </summary>
internal static class ConfigStore
{
    private const string FileName = "MoreControls.json";

    private static readonly JsonSerializerOptions SerializerOptions = new() {
        WriteIndented = true,
        // Keep CJK condition-set names readable in the config file.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>How long the config may stay dirty before it is flushed to disk.</summary>
    private const double FlushDelayMs = 750;

    public static MoreControlsConfig Config { get; private set; } = new();

    private static string _filePath = string.Empty;
    private static double _dirtySince = -1;

    /// <summary>
    /// Loads the configuration. Returns true when this is the very first run
    /// (no config file existed yet).
    /// </summary>
    public static bool Initialize()
    {
        string dir = Framework.DalamudPlugin.ConfigDirectory.FullName;
        Directory.CreateDirectory(dir);
        _filePath = Path.Join(dir, FileName);

        if (!File.Exists(_filePath)) {
            SeedDefaults();
            Save();
            return true;
        }

        try {
            string json = File.ReadAllText(_filePath);
            Config = JsonSerializer.Deserialize<MoreControlsConfig>(json, SerializerOptions) ?? new();
        } catch (Exception e) {
            Logger.Warning($"[MoreControls] Failed to read config: {e.Message}");
            Config = new();
        }

        Config.Sets ??= [];
        Config.Widgets ??= [];
        Config.Panels ??= [];

        // Repair any malformed entries that came from an older/hand-edited file.
        foreach (ConditionSetConfig set in Config.Sets) {
            set.Conditions ??= [];

            foreach (ConditionConfig cnd in set.Conditions) {
                cnd.Arg ??= new();
            }
        }

        return false;
    }

    /// <summary>Creates a small example set so the window is not empty on first use.</summary>
    private static void SeedDefaults()
    {
        Config.Sets.Add(
            new ConditionSetConfig {
                Name = "示例：非战斗中显示",
                Conditions = [
                    new ConditionConfig {
                        Id     = "cf",
                        Negate = true,
                        Arg    = new ConditionArg { Int1 = (int)Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat },
                    },
                ],
            }
        );

        MarkDirty();
    }

    public static void MarkDirty()
    {
        if (_dirtySince < 0) _dirtySince = Environment.TickCount64;
    }

    /// <summary>
    /// Called from the plugin tick. Writes the file once the config has been
    /// idle for a moment so that dragging sliders doesn't hammer the disk.
    /// </summary>
    public static void Flush()
    {
        if (_dirtySince < 0) return;
        if (Environment.TickCount64 - _dirtySince < FlushDelayMs) return;

        _dirtySince = -1;
        Save();
    }

    public static void Save()
    {
        if (string.IsNullOrEmpty(_filePath)) return;

        try {
            File.WriteAllText(_filePath, JsonSerializer.Serialize(Config, SerializerOptions));
        } catch (Exception e) {
            Logger.Warning($"[MoreControls] Failed to save config: {e.Message}");
        }
    }
}
