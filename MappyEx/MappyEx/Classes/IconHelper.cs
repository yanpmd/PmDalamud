using System;
using System.Collections.Generic;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Mappy.Data;

namespace Mappy.Classes;

/// <summary>
/// Safe access to the game icons. The icon id fields are free text, the user can type anything into them, and an id
/// that does not exist makes the texture provider fail. The map icons of the game live in the 60xxx range, everything
/// above that is a typo and must never reach the texture provider.
/// </summary>
public static class IconHelper
{
    /// <summary>
    /// Highest icon id the game uses. The map icons live in the 60xxx range, a larger value cannot be resolved.
    /// </summary>
    public const uint MaxIconId = 100000;

    /// <summary>
    /// Result of the previous lookups, the icon ids do not change while the game runs.
    /// </summary>
    private static readonly Dictionary<uint, bool> ValidityCache = [];

    /// <summary>
    /// Clamp an icon id into the range the game can resolve.
    /// </summary>
    public static uint Clamp(int iconId) => (uint)Math.Clamp(iconId, 1, (int)MaxIconId);

    /// <summary>
    /// Whether the icon id exists at all.
    /// </summary>
    public static bool IsValid(uint iconId)
    {
        if (iconId is 0 || iconId > MaxIconId) return false;

        if (ValidityCache.TryGetValue(iconId, out var cached)) return cached;

        var lookup = new GameIconLookup { IconId = iconId };

        // The plain lookup throws for an id that does not exist, the try variant reports it instead.
        var valid = Service.TextureProvider.TryGetIconPath(lookup, out _);

        ValidityCache[iconId] = valid;

        return valid;
    }

    /// <summary>
    /// Texture of an icon, null when the id cannot be resolved. The caller has to handle the missing texture.
    /// </summary>
    public static IDalamudTextureWrap? GetTexture(uint iconId)
    {
        if (!IsValid(iconId)) return null;

        var lookup = new GameIconLookup { IconId = iconId };

        if (!Service.TextureProvider.TryGetFromGameIcon(lookup, out var texture)) return null;

        return texture.GetWrapOrEmpty();
    }
}
