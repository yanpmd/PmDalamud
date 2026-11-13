using System.Globalization;
using System.Numerics;
using System.Text;
using Chat2Ex.Code;
using Chat2Ex.Util;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using Lumina.Data.Files;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;
using Chat2ExPartyFinderPayload = Chat2Ex.Util.PartyFinderPayload;
using DalamudPartyFinderPayload = Dalamud.Game.Text.SeStringHandling.Payloads.PartyFinderPayload;

namespace Chat2Ex.Native;

/// <summary>
///     The kind of interaction that triggered a link action.
/// </summary>
public enum LinkAction
{
    /// <summary>
    ///     The link was activated with the left mouse button.
    /// </summary>
    Activate,

    /// <summary>
    ///     The link (or the message row) was clicked with the right mouse button.
    /// </summary>
    Context,
}

/// <summary>
///     Turns a Chat2Ex message into a native node row. Timestamp, sender and content flow inline and
///     each rendered line becomes a single left aligned <see cref="TextNode" />, keeping the node count
///     low and the layout predictable.
/// </summary>
public sealed unsafe class MessageRenderer : IDisposable
{
    private const float IconSize = 16.0f;

    public uint FontSize => (uint)Math.Clamp(Plugin.Config.ChatFontSize, 10, 24);
    public uint LineSpacing => FontSize + 3;

    /// <summary>
    ///     Row height per rendered line. Kept clearly above the glyph height so larger font sizes do
    ///     not make consecutive rows overlap.
    /// </summary>
    public float LineHeight => FontSize * 1.5f;

    private static readonly Vector4 TimestampColor = new(0.62f, 0.62f, 0.62f, 1.0f);

    // The chat has no background, so a dark outline keeps the text readable over the world.
    private static readonly Vector4 OutlineColor = new(0.0f, 0.0f, 0.0f, 0.85f);

    /// <summary>
    ///     The texture atlas that holds the game icons (sprout, mentor crown, ...).
    /// </summary>
    private static readonly string[] IconTexturePaths =
    [
        "common/font/fonticon_ps5.tex",
        "common/font/fonticon.tex",
    ];

    /// <summary>
    ///     The width of the icon atlas in the coordinates of the game's font data. The atlas holds the
    ///     icons in a grid, the texture that the game loads is a multiple of it.
    /// </summary>
    private const float IconAtlasWidth = 512.0f;

    /// <summary>
    ///     The vertical offset of the icon atlas inside the texture.
    /// </summary>
    private const int IconAtlasOffset = 170;

    private static string? iconTexturePath;
    private static bool iconTextureResolved;
    private static bool iconDiagnosticsLogged;


    /// <summary>
    ///     Raised when the rendered messages have to be rebuilt, for example after the icon atlas
    ///     finished loading.
    /// </summary>
    public Action? RebuildRequested;

    /// <summary>
    ///     Looks up the texture that holds the game icons. The lookup only happens once, the texture
    ///     itself is loaded by the game when the node is created.
    /// </summary>
    private static bool TryGetIconTexture(out string path, out Vector2 size)
    {
        path = string.Empty;
        size = Vector2.Zero;

        if (!iconTextureResolved)
        {
            iconTextureResolved = true;

            var found = new List<string>();

            foreach (var candidate in IconTexturePaths)
            {
                if (!Plugin.DataManager.FileExists(candidate))
                    continue;

                found.Add(candidate);

                if (iconTexturePath is null)
                    iconTexturePath = candidate;
            }

            Plugin.Log.Information(
                $"[Chat2Ex] icon textures: {(found.Count == 0 ? "<none>" : string.Join(", ", found))}");
        }

        if (iconTexturePath is null)
            return false;

        path = iconTexturePath;

        var texture = Plugin.TextureProvider.GetFromGame(path).GetWrapOrDefault();
        if (texture is null)
            return false;

        size = new Vector2(texture.Width, texture.Height);
        return size.X > 0.0f;
    }

    /// <summary>
    ///     Makes sure the icon atlas is loaded. Dalamud loads the texture asynchronously, so the first
    ///     call only starts the loading and the icons are drawn once the texture is there.
    /// </summary>
    private static bool EnsureIconAtlas(out float atlasScale)
    {
        atlasScale = 0.0f;

        return iconTexturePath is not null;
    }

    /// <summary>
    ///     Writes the icon details to the log once, so that the atlas layout can be checked.
    /// </summary>
    private static void LogIconDiagnostics(BitmapFontIcon icon, GfdFileView.GfdEntry entry, string texturePath, Vector2 textureSize)
    {
        if (iconDiagnosticsLogged)
            return;

        iconDiagnosticsLogged = true;

        Plugin.Log.Information(
            $"[Chat2Ex] icon atlas: texture '{texturePath}' file {textureSize.X}x{textureSize.Y}, " +
            $"icon {icon} at {entry.Left},{entry.Top} size {entry.Width}x{entry.Height}");

        LogIconAtlasBounds();
    }

    /// <summary>
    ///     Writes the bounds of all icons to the log, so that the layout of the atlas inside the
    ///     texture can be derived.
    /// </summary>
    private static void LogIconAtlasBounds()
    {
        try
        {
            var view = IconUtil.GfdFileView;
            var maxRight = 0;
            var maxBottom = 0;
            var count = 0;
            var samples = new List<string>();

            foreach (var icon in Enum.GetValues<BitmapFontIcon>())
            {
                if (!view.TryGetEntry((uint) icon, out var entry))
                    continue;

                count++;
                maxRight = Math.Max(maxRight, entry.Left + entry.Width);
                maxBottom = Math.Max(maxBottom, entry.Top + entry.Height);

                if (samples.Count < 12)
                    samples.Add($"{icon}={entry.Left},{entry.Top},{entry.Width}x{entry.Height}");
            }

            Plugin.Log.Information(
                $"[Chat2Ex] icon atlas bounds: {count} entries, max right {maxRight}, max bottom {maxBottom}");
            Plugin.Log.Information($"[Chat2Ex] icon atlas samples: {string.Join(" | ", samples)}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[Chat2Ex] icon atlas bounds failed");
        }
    }

    private static int? iconAtlasOffsetX;
    private static int? iconAtlasOffsetY;

    /// <summary>
    ///     Finds the position of the icon atlas inside the texture. The coordinates of the game's font
    ///     data are relative to the atlas, but the atlas does not start at the top left corner of the
    ///     texture. The pixels of the texture are used to find the position that matches the most
    ///     icons.
    /// </summary>
    private static (int X, int Y) GetIconAtlasOffset(int width, int height)
    {
        if (iconAtlasOffsetX is not null && iconAtlasOffsetY is not null)
            return (iconAtlasOffsetX.Value, iconAtlasOffsetY.Value);

        iconAtlasOffsetX = 0;
        iconAtlasOffsetY = IconAtlasOffset;

        try
        {
            var path = iconTexturePath ?? "common/font/fonticon_ps5.tex";
            var file = Plugin.DataManager.GetFile<TexFile>(path);

            if (file is null)
                return (0, iconAtlasOffsetY.Value);

            var pixels = file.GetRgbaImageData();

            Plugin.Log.Information($"[Chat2Ex] icon atlas detection: {path} {width}x{height}, {pixels.Length} pixel bytes");

            if (width <= 0 || height <= 0 || pixels.Length < width * height * 4)
            {
                Plugin.Log.Information($"[Chat2Ex] icon atlas detection failed: {width}x{height}, {pixels.Length} pixel bytes");
                return (0, iconAtlasOffsetY.Value);
            }

            var view = IconUtil.GfdFileView;
            var entries = new List<GfdFileView.GfdEntry>();

            foreach (var icon in Enum.GetValues<BitmapFontIcon>())
            {
                if (view.TryGetEntry((uint) icon, out var entry))
                    entries.Add(entry);
            }

            var bestX = 0;
            var bestY = 0;
            var bestHits = 0;

            // The icons are placed in a grid of twenty pixels, so the atlas starts at a multiple of it.
            for (var oy = 0; oy + 320 <= height; oy += 20)
            {
                for (var ox = 0; ox + 260 <= width; ox += 20)
                {
                    // The first icons of the atlas are next to each other, that weeds out most of the
                    // candidates before the expensive check.
                    if (!HasContent(pixels, width, height, ox, oy, 20, 20))
                        continue;

                    if (!HasContent(pixels, width, height, ox + 20, oy, 20, 20))
                        continue;

                    if (!HasContent(pixels, width, height, ox + 40, oy, 20, 20))
                        continue;

                    var hits = 0;

                    foreach (var entry in entries)
                    {
                        if (HasContent(pixels, width, height, ox + entry.Left, oy + entry.Top, entry.Width, entry.Height))
                            hits++;
                    }

                    if (hits > bestHits)
                    {
                        bestHits = hits;
                        bestX = ox;
                        bestY = oy;
                    }
                }
            }

            if (bestHits > entries.Count / 4)
            {
                iconAtlasOffsetX = bestX;
                iconAtlasOffsetY = bestY;

                Plugin.Log.Information(
                    $"[Chat2Ex] icon atlas offset: {bestX},{bestY}, {bestHits}/{entries.Count} icons found");
            }
            else
            {
                Plugin.Log.Information($"[Chat2Ex] icon atlas offset detection failed, best {bestHits}/{entries.Count}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[Chat2Ex] icon atlas offset detection failed");
        }

        return (iconAtlasOffsetX.Value, iconAtlasOffsetY.Value);
    }

    /// <summary>
    ///     Whether the given area of the texture contains anything but transparent pixels.
    /// </summary>
    private static bool HasContent(byte[] pixels, int width, int height, int x, int y, int w, int h)
    {
        if (x < 0 || y < 0 || w <= 0 || h <= 0 || x + w > width || y + h > height)
            return false;

        for (var py = y; py < y + h; py++)
        {
            var row = py * width;

            for (var px = x; px < x + w; px++)
            {
                if (pixels[(row + px) * 4 + 3] != 0)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Rebuilds the glyph cache of a text node. The font size is only a field of the node, without
    ///     a rebuild the glyphs of the previous size are used and the text looks stretched.
    /// </summary>
    public static void ApplyFontSize(TextNode node) => node.Node->BuildFontCache();

    private readonly Plugin Plugin;
    private readonly TextNode measureNode;

    private Chunk? hoveredChunk;
    private Chunk? hoveredLink;
    private Vector2 hoveredLinkPosition;
    private long pendingTooltipCloseAt;

    public MessageRenderer(Plugin plugin)
    {
        Plugin = plugin;
        measureNode = new TextNode
        {
            FontSize = FontSize,
            LineSpacing = LineSpacing,
        };
    }

    /// <summary>
    ///     Builds a single message row. The returned node owns all of its children.
    /// </summary>
    /// <param name="message">The message to render.</param>
    /// <param name="tab">The tab the message belongs to.</param>
    /// <param name="width">The width available for the row.</param>
    /// <param name="onAction">
    ///     Called when a link is activated or when the row is right clicked. The chunk is the link
    ///     that was hit, or <c>null</c> when the row itself was clicked.
    /// </param>
    public ResNode Build(Message message, Tab tab, float width, Action<Message, Chunk?, LinkAction> onAction)
    {
        var row = new ResNode
        {
            Size = new Vector2(width, LineHeight),
        };

        var hitbox = new CollisionNode
        {
            Size = new Vector2(width, LineHeight),
        };
        hitbox.AttachNode(row);
        hitbox.AddEvent(AtkEventType.MouseDown, (AtkEventListener* _, AtkEventType _, int _, AtkEvent* _, AtkEventData* data) =>
        {
            if (data is null || !data->IsRightClick)
                return;

            NativeCallback.Safe(() => onAction(message, HoveredLinkAt(data->MousePosition), LinkAction.Context), "handling a message right click");
        });

        var writer = new LineWriter(this, row, width, message, onAction);

        if (tab.DisplayTimestamp)
        {
            var stamp = FormatTimestamp(message) + "  ";
            writer.EmitInline(stamp, TimestampColor, 0.0f);
            writer.SetIndent(Math.Min(width - 1.0f, Measure(stamp)));
        }

        // Chunks without an explicit payload colour fall back to the configured channel colour.
        var fallback = ResolveChannelColor(message);

        Chunk? previousChunk = null;
        Payload? previousLink = null;

        foreach (var chunk in MergeRuns(message))
        {
            switch (chunk)
            {
                case TextChunk text when text.Link is null:
                    writer.AppendText(text.Content, text.Color ?? fallback);
                    break;

                case TextChunk text:
                    // Clickable content gets a small marker in front of it, so that it can be told
                    // apart from ordinary text. The message may already contain the game's own icon.
                    if (NeedsLinkMarker(text, previousChunk, previousLink))
                        writer.AppendIcon(new IconChunk(text.Source, null, LinkMarkerIcon(text.Link)));

                    writer.AppendLinkTokens(text, fallback);
                    break;

                case IconChunk icon:
                    // The game draws its icons (sprout, mentor crown, ...) from a texture atlas.
                    writer.AppendIcon(icon);
                    break;
            }

            previousChunk = chunk;
            previousLink = chunk.Link;
        }

        writer.FlushLine();

        var height = Math.Max(LineHeight, writer.Bottom);
        row.Size = new Vector2(width, height);
        hitbox.Size = new Vector2(width, height);

        return row;
    }

    /// <summary>
    ///     Whether a clickable chunk needs a marker icon in front of it.
    /// </summary>
    private static bool NeedsLinkMarker(TextChunk chunk, Chunk? previousChunk, Payload? previousLink)
    {
        // The sender name is a link too, but it is not an actionable piece of content.
        if (chunk.Source == ChunkSource.Sender)
            return false;

        // Auto translate markers are already rendered as brackets.
        if (chunk.Link is AutoTranslatePayload)
            return false;

        // The game already put an icon in front of this link.
        if (previousChunk is IconChunk)
            return false;

        // A link that spans several chunks only needs one marker.
        if (previousLink is not null && ReferenceEquals(previousLink, chunk.Link))
            return false;

        return NativePayloadHandler.CanHandle(chunk.Link);
    }

    /// <summary>
    ///     The game icon that is shown in front of a clickable piece of content.
    /// </summary>
    private static BitmapFontIcon LinkMarkerIcon(Payload? link) => link switch
    {
        MapLinkPayload => BitmapFontIcon.Aetheryte,
        Chat2ExPartyFinderPayload or DalamudPartyFinderPayload => BitmapFontIcon.LookingForParty,
        ItemPayload or QuestPayload or AchievementPayload => BitmapFontIcon.GoldStar,
        _ => BitmapFontIcon.Aetheryte,
    };

    /// <summary>
    ///     Resolves the configured colour for the message's chat type, falling back to the channel's
    ///     built-in default colour.
    /// </summary>
    private Vector4? ResolveChannelColor(Message message)
    {
        var type = message.Code.Type;

        if (Plugin.Config.ChatColours.TryGetValue(type, out var rgba))
            return ColourUtil.RgbaToVector4(rgba);

        return ColourUtil.RgbaToVector4(type.DefaultColor() ?? 0);
    }

    private static readonly CultureInfo TwentyFourHourCulture = CultureInfo.CreateSpecificCulture("de-DE");

    private string FormatTimestamp(Message message)
    {
        // Creating a CultureInfo for every rendered message was a hot allocation on rebuild frames.
        var culture = Plugin.Config.Use24HourClock ? TwentyFourHourCulture : null;
        return message.Date.ToLocalTime().ToString("t", culture);
    }

    /// <summary>
    ///     Merges consecutive plain text chunks with the same style, so a line only needs one node per
    ///     colour run.
    /// </summary>
    private static List<Chunk> MergeRuns(Message message)
    {
        var chunks = new List<Chunk>(message.Sender.Count + message.Content.Count);
        TextChunk? pending = null;

        foreach (var chunk in message.Sender.Concat(message.Content))
        {
            if (chunk is TextChunk text && text.Link is null && !text.Italic)
            {
                if (pending is not null
                    && pending.Source == text.Source
                    && pending.Color == text.Color)
                {
                    pending = pending.NewWithStyle(pending.Source, null, pending.Content + text.Content);
                    continue;
                }

                Flush(chunks, ref pending);
                pending = text;
                continue;
            }

            Flush(chunks, ref pending);
            chunks.Add(chunk);
        }

        Flush(chunks, ref pending);

        return chunks;
    }

    private static void Flush(List<Chunk> chunks, ref TextChunk? pending)
    {
        if (pending is null)
            return;

        chunks.Add(pending);
        pending = null;
    }

    private readonly Dictionary<string, float> measureCache = new(StringComparer.Ordinal);
    private const int MeasureCacheLimit = 4096;

    private float Measure(string text)
    {
        // The measuring node has to follow font size changes, otherwise wrapping and the timestamp
        // indent are computed with stale metrics. Cached widths are only valid for one font size.
        if (measureNode.FontSize != FontSize)
        {
            measureNode.FontSize = FontSize;
            ApplyFontSize(measureNode);
            measureNode.LineSpacing = LineSpacing;
            measureCache.Clear();
        }

        if (measureCache.TryGetValue(text, out var cached))
            return cached;

        measureNode.String = text;
        var width = measureNode.GetTextDrawSize(false).X;

        // Chat text is unbounded; keep the cache from growing without limit.
        if (measureCache.Count >= MeasureCacheLimit)
            measureCache.Clear();

        measureCache[text] = width;
        return width;
    }

    /// <summary>
    ///     Wires up click and hover handling for a token of a chunk that carries a link.
    /// </summary>
    private void AttachLinkEvents(TextNode node, Chunk chunk, Message message, Action<Message, Chunk?, LinkAction> onAction)
    {
        if (!NativePayloadHandler.CanHandle(chunk.Link))
            return;

        node.ShowClickableCursor = true;

        node.AddEvent(AtkEventType.MouseClick, (AtkEventListener* _, AtkEventType _, int _, AtkEvent* _, AtkEventData* data) =>
        {
            // Right clicks are handled by the mouse down event below, otherwise the left click
            // action would also run for them.
            if (data is not null && data->IsRightClick)
                return;

            NativeCallback.Safe(() => onAction(message, chunk, LinkAction.Activate), "handling a link click");
        });

        node.AddEvent(AtkEventType.MouseDown, (AtkEventListener* _, AtkEventType _, int _, AtkEvent* _, AtkEventData* data) =>
        {
            if (data is null || !data->IsRightClick)
                return;

            NativeCallback.Safe(() => onAction(message, chunk, LinkAction.Context), "handling a link right click");
        });

        node.AddEvent(AtkEventType.MouseOver, (AtkEventListener* _, AtkEventType _, int _, AtkEvent* _, AtkEventData* data) =>
            NativeCallback.Safe(() => OnLinkHovered(chunk, data is null ? Vector2.Zero : data->MousePosition), "handling a link hover"));

        node.AddEvent(AtkEventType.MouseOut, () => NativeCallback.Safe(() => OnLinkUnhovered(chunk), "handling a link hover"));
    }

    /// <summary>
    ///     Returns the link that is under the given mouse position, if any. The hovered link is
    ///     only used when the cursor did not move away from it in the meantime.
    /// </summary>
    private Chunk? HoveredLinkAt(Vector2 mousePosition)
    {
        if (hoveredLink is null)
            return null;

        return (mousePosition - hoveredLinkPosition).Length() > 8.0f ? null : hoveredLink;
    }

    /// <summary>
    ///     Keeps track of the link under the cursor and opens the item tooltip for item links.
    /// </summary>
    private void OnLinkHovered(Chunk chunk, Vector2 mousePosition)
    {
        hoveredLink = chunk;
        hoveredLinkPosition = mousePosition;

        pendingTooltipCloseAt = 0;

        if (!Plugin.Config.NativeItemTooltips || chunk.Link is not ItemPayload item)
            return;

        if (ReferenceEquals(hoveredChunk, chunk))
            return;

        hoveredChunk = chunk;

        // Tooltip addons must not be touched from inside the game's UI event/draw processing.
        Plugin.Framework.RunOnTick(() => NativeCallback.Safe(() => GameFunctions.GameFunctions.OpenItemTooltip(item.RawItemId, item.Kind), "opening an item tooltip"));
    }

    /// <summary>
    ///     Clears the hovered link and schedules the tooltip close.
    /// </summary>
    private void OnLinkUnhovered(Chunk chunk)
    {
        if (ReferenceEquals(hoveredLink, chunk))
            hoveredLink = null;

        if (chunk.Link is not ItemPayload || hoveredChunk is null)
            return;

        pendingTooltipCloseAt = Environment.TickCount64 + 150;
    }

    /// <summary>
    ///     Called once per frame; closes a hovered item tooltip after a short grace period so that
    ///     moving between tokens of the same link does not make the tooltip flicker.
    /// </summary>
    public void Tick()
    {
        // The icons are hidden until the game has loaded their texture, otherwise they would show a
        // black box for a few frames.

        if (pendingTooltipCloseAt == 0 || Environment.TickCount64 < pendingTooltipCloseAt)
            return;

        pendingTooltipCloseAt = 0;
        hoveredChunk = null;

        if (Plugin.Config.NativeItemTooltips)
            Plugin.Framework.RunOnTick(() => NativeCallback.Safe(
                GameFunctions.GameFunctions.CloseItemTooltip,
                "closing an item tooltip"));
    }

    /// <summary>
    ///     Immediately closes any hovered item tooltip, e.g. when the window is hidden.
    /// </summary>
    public void CloseTooltip()
    {
        if (hoveredChunk is null)
            return;

        hoveredChunk = null;
        pendingTooltipCloseAt = 0;

        if (Plugin.Config.NativeItemTooltips)
            Plugin.Framework.RunOnTick(() => NativeCallback.Safe(
                GameFunctions.GameFunctions.CloseItemTooltip,
                "closing an item tooltip"));
    }

    public void Dispose()
    {
        measureNode.Dispose();

    }


    /// <summary>
    ///     Accumulates tokens into lines and emits one left aligned TextNode per line.
    /// </summary>
    private sealed class LineWriter
    {
        private readonly MessageRenderer renderer;
        private readonly ResNode row;
        private readonly float width;
        private readonly Message message;
        private readonly Action<Message, Chunk?, LinkAction> onAction;
        private readonly StringBuilder line = new();
        private float lineWidth;

        private Vector4? color;
        private float x;
        private float y;
        private float bottom;

        private uint FontSize => renderer.FontSize;
        private uint LineSpacing => renderer.LineSpacing;
        private float LineHeight => renderer.LineHeight;

        public LineWriter(MessageRenderer renderer, ResNode row, float width, Message message, Action<Message, Chunk?, LinkAction> onAction)
        {
            this.renderer = renderer;
            this.row = row;
            this.width = width;
            this.message = message;
            this.onAction = onAction;
        }

        public float Bottom => Math.Max(LineHeight, bottom);

        /// <summary>
        ///     Sets the horizontal offset of the current (first) line, used to keep the timestamp and
        ///     the message content on the same line.
        /// </summary>
        public void SetIndent(float indent) => x = indent;

        /// <summary>
        ///     Emits a standalone node without advancing the cursor.
        /// </summary>
        public void EmitInline(string text, Vector4? textColor, float offset)
        {
            if (text.Length == 0)
                return;

            var node = new TextNode
            {
                Position = new Vector2(offset, y),
                Size = new Vector2(Math.Max(1.0f, width - offset), LineHeight),
                FontSize = FontSize,
                LineSpacing = LineSpacing,
                AlignmentType = AlignmentType.TopLeft,
                TextOutlineColor = OutlineColor,
                String = text,
            };

            if (textColor is { } inlineColor)
                node.TextColor = inlineColor;

            ApplyFontSize(node);
            node.AttachNode(row);
            bottom = Math.Max(bottom, y + LineHeight);
        }

        /// <summary>
        ///     Appends text to the current line without touching the colour. Must be used for inline
        ///     markers (auto translate brackets), otherwise the colour change would start a new line.
        /// </summary>
        public void AppendRaw(string text)
        {
            if (text.Length == 0)
                return;

            line.Append(text);
            lineWidth += renderer.Measure(text);
        }

        /// <summary>
        ///     Appends one of the game icons (sprout, mentor crown, ...) to the current line. The icons
        ///     live in a texture atlas, the lookup table for them comes from the game's font data.
        /// </summary>
        public void AppendIcon(IconChunk chunk)
        {
            // The auto translate markers are not images, they become brackets.
            if (chunk.Icon is BitmapFontIcon.AutoTranslateBegin or BitmapFontIcon.AutoTranslateEnd)
            {
                AppendRaw(chunk.Icon == BitmapFontIcon.AutoTranslateBegin ? "[" : "]");
                return;
            }

            if (chunk.Icon == BitmapFontIcon.None)
                return;

            GfdFileView view;

            try
            {
                view = IconUtil.GfdFileView;
            }
            catch (Exception)
            {
                // The font data is not available, the icon is skipped.
                return;
            }

            if (!view.TryGetEntry((uint) chunk.Icon, out var entry))
                return;

            // Without the texture there is nothing that could be drawn, drawing it anyway would
            // show a black box instead of the icon.
            // Without the texture there is nothing that could be drawn, drawing it anyway would
            // show a black box instead of the icon.
            if (!TryGetIconTexture(out var texturePath, out var textureSize))
                return;

            // Dalamud loads the texture, so that it is ready when the icon is drawn. The game loads
            // textures asynchronously, which would show a black box for every icon.
            var wrap = Plugin.TextureProvider.GetFromGame(texturePath).GetWrapOrDefault();

            if (wrap is null)
                return;

            // The icon gets a square box that is as tall as the text.
            var iconHeight = FontSize;
            var size = new Vector2(iconHeight, iconHeight);

            if (x > 0.0f && x + size.X > width)
                FlushLine();

            // The icon comes after the text of the current line.
            EmitBufferedLine();

            var node = new ImageNode
            {
                // The icons are centered on the line, so that they line up with the text.
                Position = new Vector2(x, y + (LineHeight - size.Y) * 0.5f),
                Size = size,
            };

            node.AddPart(new Part
            {
                Id = 0,
            });

            // The texture of Dalamud is already loaded, the game would load it asynchronously and
            // draw a black box until it is ready.
            ref var partRef = ref *node.PartsList[0];
            var kernelTexture = Plugin.TextureProvider.ConvertToKernelTexture(wrap, true);

            if (kernelTexture == IntPtr.Zero)
                return;

            partRef.UldAsset->AtkTexture.KernelTexture = (Texture*) kernelTexture;
            partRef.UldAsset->AtkTexture.TextureType = TextureType.KernelTexture;

            // The coordinates of the font data are relative to a texture of the atlas width, a
            // higher resolution texture is a multiple of it.
            var atlasScale = wrap.Width / IconAtlasWidth;

            var (offsetX, offsetY) = GetIconAtlasOffset(wrap.Width, wrap.Height);

            // The game draws the icon with the size of the part, so the part has to follow the
            // font size. The part also defines the area that is taken from the texture, so the
            // icon is sampled from its centre to keep as much of it as possible.
            var sampleSize = Math.Min(iconHeight, Math.Min(entry.Width, entry.Height));
            var centerX = (offsetX + entry.Left + entry.Width * 0.5f) * atlasScale;
            var centerY = (offsetY + entry.Top + entry.Height * 0.5f) * atlasScale;

            partRef.U = (ushort) Math.Max(0.0f, centerX - sampleSize * atlasScale * 0.5f);
            partRef.V = (ushort) Math.Max(0.0f, centerY - sampleSize * atlasScale * 0.5f);
            partRef.Width = (ushort) (sampleSize * atlasScale);
            partRef.Height = (ushort) (sampleSize * atlasScale);

            node.AttachNode(row);

            LogIconDiagnostics(chunk.Icon, entry, texturePath, textureSize);
            x += size.X;
            bottom = Math.Max(bottom, y + LineHeight);
        }

        public void AppendText(string text, Vector4? textColor)
        {
            if (text.Length == 0)
                return;

            if (color != textColor)
            {
                // Keep the same line: only the colour of the following text changes.
                EmitBufferedLine();
                color = textColor;
            }

            foreach (var token in NativeTextWrap.Tokenize(text))
            {
                if (token is "\n")
                {
                    FlushLine();
                    continue;
                }

                var tokenWidth = renderer.Measure(token);

                // Incremental width: the old code rebuilt "line.ToString() + token" and re-measured
                // the entire accumulated line for every token, which was O(n^2) per row.
                if (line.Length > 0 && x + lineWidth + tokenWidth > width)
                    FlushLine();

                line.Append(token);
                lineWidth += tokenWidth;
            }
        }

        /// <summary>
        ///     Resolves the colour of a link. Clickable content is marked by an icon in front of it,
        ///     so the text keeps the colour of the message.
        /// </summary>
        private static Vector4? ResolveLinkColor(TextChunk chunk, Vector4? fallbackColor) =>
            chunk.Color ?? fallbackColor;

        /// <summary>
        ///     Linked chunks keep one node per token so every piece can carry its own events.
        /// </summary>
        public void AppendLinkTokens(TextChunk chunk, Vector4? fallbackColor)
        {
            // Emit the buffered text first so the link continues on the same line instead of starting
            // a new one.
            EmitBufferedLine();

            foreach (var token in NativeTextWrap.Tokenize(chunk.Content))
            {
                if (token is "\n")
                {
                    x = 0.0f;
                    y += LineHeight;
                    continue;
                }

                var tokenWidth = renderer.Measure(token);

                if (x > 0.0f && x + tokenWidth > width)
                {
                    x = 0.0f;
                    y += LineHeight;
                }

                var node = new TextNode
                {
                    Position = new Vector2(x, y),
                    Size = new Vector2(Math.Max(1.0f, tokenWidth), LineHeight),
                    FontSize = FontSize,
                    LineSpacing = LineSpacing,
                    AlignmentType = AlignmentType.TopLeft,
                    TextOutlineColor = OutlineColor,
                    String = token,
                };

                if (ResolveLinkColor(chunk, fallbackColor) is { } linkColor)
                    node.TextColor = linkColor;

                renderer.AttachLinkEvents(node, chunk, message, onAction);
                ApplyFontSize(node);
                node.AttachNode(row);

                x += tokenWidth;
                bottom = Math.Max(bottom, y + LineHeight);
            }
        }

        /// <summary>
        ///     Emits the buffered text as a node at the current cursor position without starting a new
        ///     line, used when a link chunk follows normal text.
        /// </summary>
        private void EmitBufferedLine()
        {
            if (line.Length == 0)
                return;

            var content = line.ToString();
            line.Clear();
            lineWidth = 0.0f;

            var textWidth = renderer.Measure(content);

            var node = new TextNode
            {
                Position = new Vector2(x, y),
                Size = new Vector2(Math.Max(1.0f, textWidth), LineHeight),
                FontSize = FontSize,
                LineSpacing = LineSpacing,
                AlignmentType = AlignmentType.TopLeft,
                TextOutlineColor = OutlineColor,
                String = content,
            };

            if (color is { } textColor)
                node.TextColor = textColor;

            ApplyFontSize(node);
            node.AttachNode(row);

            x += textWidth;
            bottom = Math.Max(bottom, y + LineHeight);
        }

        public void FlushLine()
        {
            if (line.Length == 0)
                return;

            var content = line.ToString();
            line.Clear();
            lineWidth = 0.0f;

            var node = new TextNode
            {
                Position = new Vector2(x, y),
                Size = new Vector2(Math.Max(1.0f, width - x), LineHeight),
                FontSize = FontSize,
                LineSpacing = LineSpacing,
                AlignmentType = AlignmentType.TopLeft,
                TextOutlineColor = OutlineColor,
                String = content,
            };

            if (color is { } textColor)
                node.TextColor = textColor;

            ApplyFontSize(node);
            node.AttachNode(row);
            bottom = Math.Max(bottom, y + LineHeight);

            x = 0.0f;
            y += LineHeight;
        }
    }
}
