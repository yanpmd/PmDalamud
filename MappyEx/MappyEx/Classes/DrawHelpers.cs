using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Classes;
using Lumina.Excel.Sheets;
using Mappy.Data;
using Action = System.Action;
using SeString = Dalamud.Game.Text.SeStringHandling.SeString;

namespace Mappy.Classes;

public class MarkerInfo
{
    public required Vector2 Position { get; set; }
    public required Vector2 Offset { get; set; }
    public required float Scale { get; set; }
    public uint? ObjectiveId { get; init; }
    public uint? DataId { get; set; }
    public MarkerType MarkerType { get; set; }
    public uint IconId { get; set; }
    public Func<string?>? PrimaryText { get; set; }
    public Func<string?>? SecondaryText { get; set; }
    public float? Radius { get; set; }
    public Vector4 RadiusColor { get; set; } = KnownColor.CornflowerBlue.Vector();
    public Vector4 RadiusOutlineColor { get; set; } = KnownColor.CornflowerBlue.Vector();
    public Action? OnRightClicked { get; set; }
    public Action? OnLeftClicked { get; set; }
    public bool IsDynamicMarker { get; init; }

    /// <summary>
    /// Extra multiplier applied on top of the global icon scale, used by user defined markers that carry their own scale.
    /// </summary>
    public float IconScaleMultiplier { get; init; } = 1.0f;

    /// <summary>
    /// Bypass the global per icon "allow click" setting, used by markers that are only usable through clicking.
    /// </summary>
    public bool AlwaysAllowInteraction { get; init; }

    /// <summary>
    /// The marker brings its own icon, scale and colour (user defined markers and their groups), so it must not be
    /// registered in the global icon settings where the user would have two places to change the same thing.
    /// </summary>
    public bool IgnoreIconSettings { get; init; }

    /// <summary>
    /// Popup id for the right click menu, handing over an id avoids allocating a closure per marker per frame.
    /// </summary>
    public int PopupSeed { get; init; }
}

public static class DrawHelpers
{
    private static bool DebugMode => System.SystemConfig.DebugMode;

    public const uint QuestionMarkIcon = 60071;

    /// <summary>
    /// Offset Vector of SelectedX, SelectedY, scaled with SelectedSizeFactor
    /// </summary>
    public static Vector2 GetMapOffsetVector() => GetRawMapOffsetVector() * GetMapScaleFactor();

    /// <summary>
    /// Unscaled Vector of SelectedX, SelectedY
    /// </summary>
    public static unsafe Vector2 GetRawMapOffsetVector() => new(AgentMap.Instance()->SelectedOffsetX, AgentMap.Instance()->SelectedOffsetY);

    /// <summary>
    /// Selected Scale Factor
    /// </summary>
    public static unsafe float GetMapScaleFactor() => AgentMap.Instance()->SelectedMapSizeFactorFloat;

    /// <summary>
    /// 1024 vector, center offset vector
    /// </summary>
    public static Vector2 GetMapCenterOffsetVector() => new(1024.0f, 1024.0f);

    /// <summary>
    /// Offset for the top left corner of the drawn map
    /// </summary>
    public static Vector2 GetCombinedOffsetVector() => -GetMapOffsetVector() + GetMapCenterOffsetVector();

    public static void DrawMapMarker(MarkerInfo markerInfo)
    {
        if (markerInfo.IconId is 0) return;

        // Don't draw markers that are positioned off the map texture
        if (markerInfo.Position.X < 0.0f || markerInfo.Position.X > 2048.0f * markerInfo.Scale || markerInfo.Position.Y < 0.0f ||
            markerInfo.Position.Y > 2048.0f * markerInfo.Scale)
            return;

        markerInfo.IconId = markerInfo.IconId switch
        {
            // Translate circle markers that don't have icons, into [?] icon
            >= 60483 and <= 60494 => QuestionMarkIcon,

            // Translate Gemstone Trader Icon into smaller version... why square, why.
            60091 => 61731,

            // Leave all other icons as they were
            _ => markerInfo.IconId,
        };

        if (DebugMode) {
            markerInfo.SecondaryText = markerInfo.PrimaryText;
            markerInfo.PrimaryText = () => Localization.F("Marker.DebugIconId", markerInfo.IconId);
        }

        // If this icon is disabled, don't even process it
        if (GetIconSetting(markerInfo) is { Hide: true }) {
            return;
        }

        // Only process modules for Dynamic Markers
        if (markerInfo.IsDynamicMarker) {
            foreach (var module in System.Modules) {
                if (module.ProcessMarker(markerInfo)) {
                    break;
                }
            }
        }

        DrawRadiusUnderlay(markerInfo);
        DrawIcon(markerInfo);
        ProcessInteractions(markerInfo);
        DrawTooltip(markerInfo);
    }

    /// <summary>
    /// Settings for markers that do not participate in the icon list, always the neutral defaults.
    /// </summary>
    private static readonly IconSetting DefaultIconSetting = new() { IconId = 0 };

    /// <summary>
    /// Per icon settings, registering unknown icons on first sight so they show up in the settings window.
    /// Markers that carry their own appearance skip the registry entirely.
    /// </summary>
    private static IconSetting GetIconSetting(MarkerInfo markerInfo)
    {
        if (markerInfo.IgnoreIconSettings) return DefaultIconSetting;

        if (System.IconConfig.IconSettingMap.TryAdd(markerInfo.IconId, new IconSetting { IconId = markerInfo.IconId })) {
            System.IconConfig.Save();
        }

        return System.IconConfig.IconSettingMap[markerInfo.IconId];
    }

    private static unsafe void DrawRadiusUnderlay(MarkerInfo markerInfo)
    {
        if (markerInfo is not { Radius: { } markerRadius and > 1.0f }) return;

        var center = markerInfo.Position + markerInfo.Offset + ImGui.GetWindowPos();
        var radius = markerRadius * markerInfo.Scale * AgentMap.Instance()->SelectedMapSizeFactorFloat;
        var fillColor = ImGui.GetColorU32(markerInfo.RadiusColor with { W = System.SystemConfig.AreaColor.W });
        var radiusColor = ImGui.GetColorU32(markerInfo.RadiusOutlineColor with { W = System.SystemConfig.AreaOutlineColor.W });

        ImGui.GetWindowDrawList().AddCircleFilled(center, radius, fillColor);
        ImGui.GetWindowDrawList().AddCircle(center, radius, radiusColor, 0, 3.0f);
    }

    private static void DrawIcon(MarkerInfo markerInfo)
    {
        // A wrong icon id must never reach the texture provider, the marker is drawn without an image instead.
        var texture = IconHelper.GetTexture(markerInfo.IconId);
        var scale = System.SystemConfig.ScaleWithZoom ? markerInfo.Scale : 1.0f;

        var iconScale = System.SystemConfig.IconScale;

        if (markerInfo.IconId is 60401 or 60402) {
            scale *= 2.0f;
        }

        // Fixed scale not supported for map region markers
        if (IsRegionIcon(markerInfo.IconId)) {
            scale = markerInfo.Scale;
            iconScale = 0.42f;
        }

        var iconSetting = GetIconSetting(markerInfo);
        var settingScale = iconSetting.Scale * markerInfo.IconScaleMultiplier;

        // A placeholder keeps the marker clickable and gives the invalid icon a visible spot on the map.
        var size = texture is null ? new Vector2(ImGui.GetFontSize()) : texture.Size * scale * iconScale * settingScale;

        ImGui.SetCursorPos(markerInfo.Position + markerInfo.Offset - size / 2.0f);
        var cursorScreenPos = ImGui.GetCursorScreenPos();

        if (texture is not null) {
            ImGui.Image(texture.Handle, size, Vector2.Zero, Vector2.One, iconSetting.Color);
        }
        else {
            ImGui.Dummy(size);
        }

        if (DebugMode) {
            foreach (var x in Enumerable.Range(-1, 3)) {
                foreach (var y in Enumerable.Range(-1, 3)) {
                    ImGui.GetWindowDrawList().AddRect(cursorScreenPos + new Vector2(x, y), cursorScreenPos + size, ImGui.GetColorU32(KnownColor.White.Vector()), 3.0f);
                }
            }

            ImGui.GetWindowDrawList().AddRect(cursorScreenPos, cursorScreenPos + size, ImGui.GetColorU32(KnownColor.Red.Vector()), 3.0f);
        }
    }

    public static void DrawText(MarkerInfo markerInfo, SeString text) => DrawText(markerInfo, text.ToString());

    public static void DrawText(MarkerInfo markerInfo, string text)
    {
        using var largeFont = System.LargeAxisFontHandle.Push();
        ImGui.SetWindowFontScale(markerInfo.Scale);

        var textSize = ImGui.CalcTextSize(text);
        var drawPosition = markerInfo.Position + markerInfo.Offset + ImGui.GetWindowPos() - textSize / 2.0f;

        drawPosition = new Vector2(MathF.Round(drawPosition.X), MathF.Round(drawPosition.Y));

        if (System.SystemConfig.DebugMode) {
            ImGui.GetWindowDrawList().AddCircleFilled(markerInfo.Position + markerInfo.Offset + ImGui.GetWindowPos(), 5.0f, ImGui.GetColorU32(KnownColor.Red.Vector()));
            ImGui.GetWindowDrawList().AddRect(drawPosition, drawPosition + textSize, ImGui.GetColorU32(KnownColor.Green.Vector()), 3.0f);
        }

        foreach (var x in Enumerable.Range(-1, 3)) {
            foreach (var y in Enumerable.Range(-1, 3)) {
                if (x is 0 && y is 0) continue;

                ImGui.SetCursorScreenPos(drawPosition + new Vector2(x, y));
                ImGui.TextColored(KnownColor.Black.Vector(), text);
            }
        }

        ImGui.SetCursorScreenPos(drawPosition);
        ImGui.TextColored(KnownColor.White.Vector(), text);

        ImGui.SetWindowFontScale(1.0f);
    }

    private static void ProcessInteractions(MarkerInfo markerInfo)
    {
        // Context menus are always reachable, the per icon "allow click" setting only guards the left click actions.
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            if (markerInfo.OnRightClicked is { } rightClickAction) {
                rightClickAction.Invoke();
            }
            else if (markerInfo.PopupSeed is not 0) {
                MarkerContextMenu.Open(markerInfo.PopupSeed);
            }
        }

        if (!markerInfo.AlwaysAllowInteraction && GetIconSetting(markerInfo) is not { AllowClick: true }) return;

        if (markerInfo is { OnLeftClicked: { } leftClickAction } && ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
            leftClickAction.Invoke();
        }
    }

    private static unsafe void DrawTooltip(MarkerInfo markerInfo)
    {
        if (GetIconSetting(markerInfo) is { AllowTooltip: false } && !DebugMode) {
            return;
        }

        var isActivatedViaRadius = false;

        if (markerInfo is { Radius: { } sameRadius and > 1.0f }) {
            var center = markerInfo.Position + markerInfo.Offset + ImGui.GetWindowPos();
            var radius = sameRadius * markerInfo.Scale * AgentMap.Instance()->SelectedMapSizeFactorFloat;

            if (Vector2.Distance(ImGui.GetMousePos() - System.MapWindow.MapDrawOffset + ImGui.GetWindowPos(), center) <= radius && System.MapWindow.HoveredFlags.Any()) {
                isActivatedViaRadius = true;
            }
        }

        if (isActivatedViaRadius || ImGui.IsItemHovered()) {
            if (markerInfo.PrimaryText?.Invoke() is { Length: > 0 } primaryText) {
                using var tooltip = ImRaii.Tooltip();

                // The icon may not exist, the tooltip still works without the image.
                if (IconHelper.GetTexture(markerInfo.IconId) is { } iconTexture) {
                    ImGui.Image(iconTexture.Handle, ImGuiHelpers.ScaledVector2(32.0f, 32.0f));
                }
                else {
                    ImGui.Dummy(ImGuiHelpers.ScaledVector2(32.0f, 32.0f));
                }

                ImGui.SameLine();
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 7.5f * ImGuiHelpers.GlobalScale);
                var cursorPosition = ImGui.GetCursorPos();
                ImGui.Text(primaryText);

                if (markerInfo.SecondaryText?.Invoke() is { Length: > 0 } secondaryText) {
                    ImGui.SameLine();
                    ImGui.SetCursorPos(cursorPosition);
                    ImGuiTweaks.TextColoredUnformatted(KnownColor.Gray.Vector(), $"\n{secondaryText}");
                }
            }
        }
    }

    public static bool IsDisallowedIcon(uint iconId) =>
        iconId switch
        {
            60091 => true,
            _ when IsRegionIcon(iconId) => true,
            _ => false,
        };

    public static bool IsRegionIcon(uint iconId) =>
        iconId switch
        {
            >= 63200 and < 63900 => true,
            >= 62620 and < 62800 => true,
            _ => false,
        };

    /// <summary>
    /// Content centers are expensive to compute, the excel sheets do not change while the game is running.
    /// </summary>
    private static readonly Dictionary<uint, Vector2?> ContentCenterCache = [];

    /// <summary>
    /// Loading screen picture of a map, used wherever a map is previewed.
    /// </summary>
    public static IDalamudTextureWrap? GetMapThumbnail(uint mapId)
    {
        if (mapId is 0) return null;

        var map = Service.DataManager.GetExcelSheet<Map>().GetRow(mapId);
        if (map.RowId is 0) return null;

        var territory = map.TerritoryType;
        if (!territory.IsValid) return null;

        var loadingImage = territory.Value.LoadingImage;
        if (!loadingImage.IsValid) return null;

        return Service.TextureProvider.GetFromGame($"ui/loadingimage/{loadingImage.Value.FileName}_hr1.tex").GetWrapOrDefault();
    }

    /// <summary>
    /// Center of the part of the map that actually holds content, in texture pixels.
    /// Many maps only fill a corner of their texture, centering the texture itself would leave the user on empty background.
    /// </summary>
    public static bool TryGetMapContentCenter(uint mapId, out Vector2 center)
    {
        center = default;

        if (mapId is 0) return false;

        if (ContentCenterCache.TryGetValue(mapId, out var cached)) {
            if (cached is not { } cachedCenter) return false;

            center = cachedCenter;

            return true;
        }

        var map = Service.DataManager.GetExcelSheet<Map>().GetRow(mapId);
        var computed = TryGetMapContentCenter(map, out var computedCenter) ? computedCenter : (Vector2?)null;

        ContentCenterCache[mapId] = computed;

        if (computed is not { } result) return false;

        center = result;

        return true;
    }

    private static bool TryGetMapContentCenter(Map map, out Vector2 center)
    {
        center = default;

        var markerRange = (uint)map.MapMarkerRange;
        if (map.RowId is 0 || markerRange is 0) return false;

        var textureSize = GetMapCenterOffsetVector() * 2.0f;

        List<Vector2> namedPlaces = [];
        List<Vector2> allMarkers = [];

        foreach (var marker in Service.DataManager.GetSubrowExcelSheet<MapMarker>().SelectMany(collection => collection)) {
            if (marker.RowId != markerRange) continue;

            // Excel marker positions are already map texture pixels, the agent scales them by 16 instead.
            var position = new Vector2(marker.X, marker.Y);
            if (position.X < 0.0f || position.Y < 0.0f || position.X > textureSize.X || position.Y > textureSize.Y) continue;

            allMarkers.Add(position);

            // Named places describe where the map is actually used, plain markers can sit far outside the playable part.
            if (marker.PlaceNameSubtext.RowId is not 0) namedPlaces.Add(position);
        }

        var candidates = namedPlaces.Count > 0 ? namedPlaces : allMarkers;
        if (candidates.Count is 0) return false;

        // Median instead of a mean, a single stray marker must not drag the view away from the content.
        var centerPosition = new Vector2(Median(candidates.Select(position => position.X)), Median(candidates.Select(position => position.Y)));

        center = Vector2.Clamp(centerPosition, Vector2.Zero, textureSize);

        return true;
    }

    private static float Median(IEnumerable<float> values)
    {
        var sorted = values.OrderBy(value => value).ToList();
        if (sorted.Count is 0) return 0.0f;

        var middle = sorted.Count / 2;

        return sorted.Count % 2 is 0 ? (sorted[middle - 1] + sorted[middle]) / 2.0f : sorted[middle];
    }
}