using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Umbra.Common;

namespace Umbra.QoLBar;

/// <summary>
/// Loads and caches icons used by shortcuts. Game icons are resolved through
/// Lumina's ui/icon/ paths; user icons are PNG files stored in the plugin's
/// "icons" configuration folder (negative icon IDs).
/// </summary>
[Service]
public class IconManager
{
    private const string IconFileFormat = "ui/icon/{0:D3}000/{1}{2:D6}{3}.tex";

    private readonly Dictionary<int, byte[]> userIconCache = new();
    private readonly Dictionary<(uint icon, bool hr), IDalamudTextureWrap> textureCache = new();
    private readonly Dictionary<int, IDalamudTextureWrap> userIconWrapCache = new();

    public static IconManager Instance { get; private set; }

    public IconManager()
    {
        Instance = this;
    }

    /// <summary>
    /// Returns the folder in which user icons are stored, creating it if needed.
    /// </summary>
    public static DirectoryInfo GetUserIconFolder()
    {
        var dir = new DirectoryInfo(Path.Combine(Framework.DalamudPlugin.ConfigDirectory.FullName, "QoLBar", "icons"));
        if (!dir.Exists) dir.Create();
        return dir;
    }

    /// <summary>
    /// Returns a list of user icons found in the icons folder, keyed by their
    /// (negative) icon ID.
    /// </summary>
    public Dictionary<int, string> GetUserIcons()
    {
        var result = new Dictionary<int, string>();

        try
        {
            foreach (var file in GetUserIconFolder().GetFiles("*.png").OrderBy(f => f.Name))
            {
                if (!int.TryParse(Path.GetFileNameWithoutExtension(file.Name), out var i) || i <= 0) continue;
                result[-i] = file.FullName;
            }
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to scan user icons: {e.Message}");
        }

        return result;
    }

    /// <summary>
    /// Returns the raw image bytes of a user icon, or null when unavailable.
    /// </summary>
    public byte[] GetUserIconBytes(int iconId)
    {
        if (iconId >= 0) return null;

        if (userIconCache.TryGetValue(iconId, out var cached)) return cached;

        var icons = GetUserIcons();
        if (!icons.TryGetValue(iconId, out var path) || !File.Exists(path)) return null;

        try
        {
            var bytes = File.ReadAllBytes(path);
            userIconCache[iconId] = bytes;
            return bytes;
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to load user icon {path}: {e.Message}");
            return null;
        }
    }

    public void ClearUserIconCache()
    {
        userIconCache.Clear();

        foreach (var wrap in userIconWrapCache.Values)
        {
            try
            {
                wrap?.Dispose();
            }
            catch
            {
                // The texture may already have been released by Dalamud.
            }
        }

        userIconWrapCache.Clear();
        textureCache.Clear();
    }

    /// <summary>
    /// Textures handed out by Dalamud can be released at any time, after which
    /// accessing their handle throws. Returns true only if the given texture is
    /// still usable.
    /// </summary>
    public static bool TryGetTexture(IDalamudTextureWrap wrap, out IDalamudTextureWrap texture)
    {
        texture = wrap;

        if (wrap == null) return false;

        try
        {
            _ = wrap.Handle;
            return true;
        }
        catch
        {
            texture = null;
            return false;
        }
    }

    private static string GetIconPath(uint icon, string language, bool hr) => string.Format(IconFileFormat, icon / 1000, language, icon, hr ? "_hr1" : string.Empty);

    private static string GetLanguagePath(ClientLanguage language) => language switch
    {
        ClientLanguage.Japanese => "ja/",
        ClientLanguage.English => "en/",
        ClientLanguage.German  => "de/",
        ClientLanguage.French  => "fr/",
        _                      => "en/"
    };

    /// <summary>
    /// Returns true if an icon with the given ID exists in the game data.
    /// </summary>
    public static bool IconExists(uint icon)
    {
        var dataManager = Framework.Service<IDataManager>();
        return dataManager.FileExists(GetIconPath(icon, string.Empty, false))
            || dataManager.FileExists(GetIconPath(icon, "en/", false));
    }

    /// <summary>
    /// Returns a cached game icon texture, loading it on first use.
    /// Returns null when the icon does not exist.
    /// </summary>
    public IDalamudTextureWrap GetGameIcon(uint icon, bool hr = false)
    {
        if (textureCache.TryGetValue((icon, hr), out var cached))
        {
            // Dalamud may release the underlying texture at any point, which
            // invalidates the cached wrap.
            if (TryGetTexture(cached, out _)) return cached;

            textureCache.Remove((icon, hr));
        }

        var lookup = new GameIconLookup { IconId = icon, HiRes = hr };
        var texture = Framework.Service<ITextureProvider>().GetFromGameIcon(in lookup);

        if (texture == null || !texture.TryGetWrap(out var wrap, out _) || !TryGetTexture(wrap, out _)) return null;

        if (textureCache.Count > 600) textureCache.Clear();

        textureCache[(icon, hr)] = wrap;
        return wrap;
    }

    /// <summary>
    /// Returns a cached texture for a user icon (negative icon ID), or null
    /// when the icon does not exist.
    /// </summary>
    public IDalamudTextureWrap GetUserIconWrap(int iconId)
    {
        if (iconId >= 0) return null;

        if (userIconWrapCache.TryGetValue(iconId, out var cached))
        {
            if (TryGetTexture(cached, out _)) return cached;

            userIconWrapCache.Remove(iconId);
        }

        var icons = GetUserIcons();
        if (!icons.TryGetValue(iconId, out var path) || !File.Exists(path)) return null;

        try
        {
            using var stream = File.OpenRead(path);
            var wrap = Framework.Service<ITextureProvider>().CreateFromImageAsync(stream).Result;

            if (!TryGetTexture(wrap, out _)) return null;

            userIconWrapCache[iconId] = wrap;
            return wrap;
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to load user icon {path}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses the display syntax of a shortcut name.
    /// Supports "Name##Tooltip", "SubName::IconID" and icon arguments
    /// (f=frame, n=no frame, l=LR, h=HR, g=grayscale, r=flip).
    /// </summary>
    public static bool ParseName(ref string name, out string subName, out string tooltip, out int icon, out string args)
    {
        subName = string.Empty;
        args = string.Empty;
        if (name == string.Empty)
        {
            tooltip = string.Empty;
            icon = 0;
            return false;
        }

        var split = name.Split(new[] { "##" }, 2, StringSplitOptions.None);
        name = split[0];

        tooltip = (split.Length > 1) ? split[1] : string.Empty;

        icon = 0;

        var split2 = name.Split(new[] { "::" }, 2, StringSplitOptions.None);
        if (split2.Length < 2) return false;

        subName = split2[0];
        name = split2[1];

        var substart = 0;

        // Parse icon arguments
        var done = false;
        while (!done)
        {
            if (name.Length > substart)
            {
                var arg = name[substart];
                switch (arg)
                {
                    case 'f': // Frame
                    case 'n': // No frame
                    case 'l': // LR icon
                    case 'h': // HR icon
                    case 'g': // Grayscale
                    case 'r': // Reverse
                        args += arg;
                        substart++;
                        break;
                    default:
                        done = true;
                        break;
                }
            }
            else
                done = true;
        }

        _ = int.TryParse(name[substart..], out icon);
        return true;
    }

    /// <summary>
    /// Rewrites the icon part of a shortcut name, preserving its sub name,
    /// tooltip and icon arguments.
    /// </summary>
    public static void SetIconId(ref string name, int iconId)
    {
        var subName = string.Empty;
        var tooltip = string.Empty;
        var args = string.Empty;

        var split = name.Split(new[] { "##" }, 2, StringSplitOptions.None);
        var namePart = split[0];
        if (split.Length > 1) tooltip = split[1];

        var split2 = namePart.Split(new[] { "::" }, 2, StringSplitOptions.None);
        if (split2.Length >= 2)
        {
            subName = split2[0];

            // Preserve existing icon arguments.
            var iconPart = split2[1];
            var substart = 0;
            var done = false;
            while (!done && iconPart.Length > substart)
            {
                switch (iconPart[substart])
                {
                    case 'f':
                    case 'n':
                    case 'l':
                    case 'h':
                    case 'g':
                    case 'r':
                        args += iconPart[substart];
                        substart++;
                        break;
                    default:
                        done = true;
                        break;
                }
            }
        }

        name = $"{(string.IsNullOrEmpty(subName) ? string.Empty : subName + "::")}::{args}{iconId}";
        if (!string.IsNullOrEmpty(tooltip)) name += $"##{tooltip}";
    }
}
