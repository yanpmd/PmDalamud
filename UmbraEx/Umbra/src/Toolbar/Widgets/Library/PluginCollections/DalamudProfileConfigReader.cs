using System.IO;
using System.Text.Json;

namespace Umbra.Widgets.Library.PluginCollections;

/// <summary>
/// Fallback reader that gets the known plugin collections straight out of
/// Dalamud's own configuration file. This is used when the live collection
/// manager cannot be reached through reflection, and only provides what is
/// persisted on disk (names and their last known state).
/// </summary>
internal static class DalamudProfileConfigReader
{
    private const string ConfigFileName = "dalamudConfig.json";

    private static bool                    _searched;
    private static string?                 _configPath;
    private static DateTime                _lastWriteTime = DateTime.MinValue;
    private static IReadOnlyList<DalamudProfile> _cache  = [];

    public static bool IsAvailable
    {
        get {
            FindConfigFile();

            return _configPath is not null;
        }
    }

    public static IReadOnlyList<DalamudProfile> Read()
    {
        FindConfigFile();

        if (_configPath is null) return [];

        try {
            DateTime writeTime = File.GetLastWriteTimeUtc(_configPath);

            if (writeTime == _lastWriteTime) return _cache;

            IReadOnlyList<DalamudProfile> profiles = Parse(_configPath);

            _lastWriteTime = writeTime;
            _cache         = profiles;

            return profiles;
        } catch (Exception e) {
            Logger.Warning($"Failed to read plugin collections from Dalamud's config: {e.Message}");

            return [];
        }
    }

    private static IReadOnlyList<DalamudProfile> Parse(string path)
    {
        using FileStream   stream   = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement        root     = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object) return [];

        foreach (JsonProperty property in root.EnumerateObject()) {
            if (!property.Name.Equals("SavedProfiles", StringComparison.OrdinalIgnoreCase)
                && !property.Name.Equals("PluginProfiles", StringComparison.OrdinalIgnoreCase)
                && !property.Name.Equals("PluginCollections", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            // Dalamud wraps polymorphic lists in a {"$type": ..., "$values": []} object.
            JsonElement list = property.Value;

            if (list.ValueKind == JsonValueKind.Object) {
                list = GetProperty(list, "$values") ?? default;
            }

            if (list.ValueKind != JsonValueKind.Array) continue;

            List<DalamudProfile> result = [];

            foreach (JsonElement entry in list.EnumerateArray()) {
                if (entry.ValueKind != JsonValueKind.Object) continue;

                // ProfileModelV1 is persisted with shortened property names.
                string name      = ReadString(entry, "Name", "n");
                string guid      = ReadString(entry, "Guid", "id");
                bool   enabled   = ReadBool(entry, "IsEnabled", "Enabled", "e") ?? false;
                bool   isDefault = ReadBool(entry, "IsDefaultProfile", "IsDefault", "d") ?? false;

                if (string.IsNullOrWhiteSpace(name)) continue;

                // Skip Dalamud's built-in default collection.
                if (guid == "00000000-0000-0000-0000-000000000000") continue;
                if (name.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase)) continue;

                result.Add(new(name, enabled, isDefault));
            }

            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

            return result;
        }

        return [];
    }

    private static JsonElement? GetProperty(JsonElement element, string propertyName)
    {
        foreach (JsonProperty property in element.EnumerateObject()) {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)) return property.Value;
        }

        return null;
    }

    private static string ReadString(JsonElement element, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames) {
            if (GetProperty(element, propertyName) is not { } value) continue;

            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
        }

        return string.Empty;
    }

    private static bool? ReadBool(JsonElement element, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames) {
            if (GetProperty(element, propertyName) is not { } value) continue;

            return value.ValueKind switch {
                JsonValueKind.True  => true,
                JsonValueKind.False => false,
                _                   => null,
            };
        }

        return null;
    }

    /// <summary>
    /// Walks up from the plugin's own directories until Dalamud's config file
    /// is found (the exact layout differs between launchers and forks).
    /// </summary>
    private static void FindConfigFile()
    {
        if (_searched) return;
        _searched = true;

        foreach (string? candidate in GetCandidateDirectories()) {
            try {
                string path = Path.Combine(candidate, ConfigFileName);

                if (File.Exists(path)) {
                    _configPath = path;

                    Logger.Debug($"Reading plugin collections from {path}");

                    return;
                }
            } catch {
                // Invalid path, keep looking.
            }
        }

        Logger.Warning($"Unable to find {ConfigFileName}; plugin collections cannot be read from disk.");
    }

    private static IEnumerable<string> GetCandidateDirectories()
    {
        List<string?> roots = [];

        try {
            roots.Add(Framework.DalamudPlugin.ConfigDirectory?.FullName);
        } catch {
            // Not available, ignore.
        }

        try {
            roots.Add(Framework.DalamudPlugin.AssemblyLocation?.Directory?.FullName);
        } catch {
            // Not available, ignore.
        }

        foreach (string? root in roots) {
            if (string.IsNullOrEmpty(root)) continue;

            DirectoryInfo? directory = new(root);

            for (var i = 0; i < 5 && directory is not null; i++) {
                yield return directory.FullName;

                directory = directory.Parent;
            }
        }

        // Generic fallback: any Dalamud-flavoured folder under %APPDATA%.
        string? roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrEmpty(roaming) || !Directory.Exists(roaming)) yield break;

        foreach (string directory in Directory.EnumerateDirectories(roaming, "XIVLauncher*")) {
            yield return directory;
        }
    }
}
