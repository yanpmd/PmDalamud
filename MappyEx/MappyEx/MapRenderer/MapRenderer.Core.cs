using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Data.Files;
using Mappy.Classes;

namespace Mappy.MapRenderer;

public unsafe partial class MapRenderer : IDisposable
{
    public static float Scale
    {
        get => System.SystemConfig.MapScale;
        set => System.SystemConfig.MapScale = value;
    }

    public Vector2 DrawOffset { get; set; }
    public Vector2 DrawPosition { get; private set; }

    /// <summary>
    /// Map the draw offset was last positioned for, used to notice map switches.
    /// </summary>
    private uint centeredMapId;

    /// <summary>
    /// Position inside a map that should be centered as soon as the game shows that map.
    /// </summary>
    private (uint MapId, Vector2 Position)? pendingFocus;

    private int pendingFocusAge;

    private IDalamudTextureWrap? blendedTexture;
    private string blendedPath = string.Empty;

    public MapRenderer()
    {
        LoadFogHooks();
    }

    public void Dispose()
    {
        UnloadFogHooks();
    }

    public void CenterOnGameObject(IGameObject obj) => CenterOnCoordinate(new Vector2(obj.Position.X, obj.Position.Z));

    public void CenterOnCoordinate(Vector2 coord)
    {
        DrawOffset = -coord * DrawHelpers.GetMapScaleFactor() + DrawHelpers.GetMapOffsetVector();

        centeredMapId = AgentMap.Instance()->SelectedMapId;
    }

    /// <summary>
    /// Centers on a specific position of a map, used when a place inside the map was picked.
    /// The position is reapplied once the game actually switched to that map, it lags behind the click.
    /// </summary>
    public void RequestTextureFocus(uint mapId, Vector2 texturePosition)
    {
        pendingFocus = (mapId, texturePosition);
        pendingFocusAge = 0;

        DrawOffset = DrawHelpers.GetMapCenterOffsetVector() - texturePosition;
        centeredMapId = mapId;
    }

    /// <summary>
    /// Keeps the automatic centering from moving a view that was placed on purpose, like a quest or flag marker.
    /// </summary>
    public void MarkMapCentered(uint mapId) => centeredMapId = mapId;

    public void MarkCurrentMapCentered() => centeredMapId = AgentMap.Instance()->SelectedMapId;

    /// <summary>
    /// Centers on the part of the map that actually holds content, many maps only fill a corner of their texture.
    /// </summary>
    public void CenterOnMapContent(uint mapId)
    {
        DrawOffset = DrawHelpers.TryGetMapContentCenter(mapId, out var center)
            ? DrawHelpers.GetMapCenterOffsetVector() - center
            : Vector2.Zero;

        centeredMapId = mapId;
    }

    /// <summary>
    /// Re-centers the view after the game switched to another map, so a map does not open on empty background.
    /// </summary>
    public void UpdateMapCentering()
    {
        var mapId = AgentMap.Instance()->SelectedMapId;

        if (pendingFocus is { } focus && focus.MapId == mapId) {
            DrawOffset = DrawHelpers.GetMapCenterOffsetVector() - focus.Position;
            centeredMapId = mapId;
            pendingFocus = null;

            return;
        }

        if (pendingFocus is not null) {
            // Waiting for the game to switch maps, give up if that never happens.
            if (pendingFocusAge++ < 300) return;

            pendingFocus = null;
        }

        if (mapId == centeredMapId) return;

        // Following the player and the locked center mode both position the map on their own.
        if (System.SystemConfig.FollowPlayer || System.SystemConfig.LockCenterOnMap) {
            centeredMapId = mapId;

            return;
        }

        CenterOnMapContent(mapId);
    }

    public void DrawBaseTexture()
    {
        UpdateScaleLimits();
        UpdateDrawOffset();

        DrawBackgroundTexture();
    }

    public void DrawDynamicElements()
    {
        DrawFogOfWar();
        DrawCoordinateGrid();
        DrawMapMarkers();
    }

    private void UpdateScaleLimits() => Scale = Math.Clamp(Scale, 0.05f, 20.0f);

    private void UpdateDrawOffset()
    {
        var childCenterOffset = ImGui.GetContentRegionAvail() / 2.0f;
        var mapCenterOffset = new Vector2(1024.0f, 1024.0f) * Scale;

        DrawPosition = childCenterOffset - mapCenterOffset + DrawOffset * Scale;
    }

    private void DrawBackgroundTexture()
    {
        if (AgentMap.Instance()->SelectedMapBgPath.Length is 0) {
            var texture = Service.TextureProvider.GetFromGame($"{AgentMap.Instance()->SelectedMapPath.ToString()}.tex").GetWrapOrEmpty();

            ImGui.SetCursorPos(DrawPosition);
            ImGui.Image(texture.Handle, texture.Size * Scale);
        }
        else {
            if (blendedPath != AgentMap.Instance()->SelectedMapBgPath.ToString()) {
                fogTexture = null;
                blendedTexture?.Dispose();
                blendedTexture = LoadTexture();
                blendedPath = AgentMap.Instance()->SelectedMapBgPath.ToString();
            }

            if (blendedTexture is not null) {
                ImGui.SetCursorPos(DrawPosition);
                ImGui.Image(blendedTexture.Handle, blendedTexture.Size * Scale);
            }
        }
    }

    private static IDalamudTextureWrap? LoadTexture()
    {
        var vanillaBgPath = $"{AgentMap.Instance()->SelectedMapBgPath.ToString()}.tex";
        var vanillaFgPath = $"{AgentMap.Instance()->SelectedMapPath.ToString()}.tex";

        var bgFile = GetTexFile(vanillaBgPath);
        var fgFile = GetTexFile(vanillaFgPath);

        if (bgFile is null || fgFile is null) {
            Service.Log.Warning("Failed to load map textures");
            return null;
        }

        var backgroundBytes = bgFile.GetRgbaImageData();
        var foregroundBytes = fgFile.GetRgbaImageData();

        // Blend textures together
        Parallel.For(0, 2048 * 2048, i =>
        {
            var index = i * 4;

            // Blend, R, G, B, skip A.
            backgroundBytes[index + 0] = (byte)(backgroundBytes[index + 0] * foregroundBytes[index + 0] / 255);
            backgroundBytes[index + 1] = (byte)(backgroundBytes[index + 1] * foregroundBytes[index + 1] / 255);
            backgroundBytes[index + 2] = (byte)(backgroundBytes[index + 2] * foregroundBytes[index + 2] / 255);
        });

        return Service.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(2048, 2048), backgroundBytes);
    }

    private static TexFile? GetTexFile(string rawPath)
    {
        var path = Service.TextureSubstitutionProvider.GetSubstitutedPath(rawPath);

        if (Path.IsPathRooted(path)) {
            return Service.DataManager.GameData.GetFileFromDisk<TexFile>(path);
        }

        return Service.DataManager.GetFile<TexFile>(path);
    }

    private void DrawMapMarkers()
    {
        DrawStaticMapMarkers();
        DrawDynamicMarkers();
        DrawGameObjects();
        DrawGroupMembers();
        DrawTemporaryMarkers();
        DrawGatheringMarkers();
        DrawFieldMarkers();
        DrawPlayer();
        DrawStaticTextMarkers();
        DrawFlag();
        DrawCustomMarkers();
    }
}