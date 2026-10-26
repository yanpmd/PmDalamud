using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace Una.Drawing.Texture;

public struct GfdIcon
{
    public SKImage Texture { get; set; }
    public Vector2 Uv0     { get; set; }
    public Vector2 Uv1     { get; set; }
    public Vector2 Size    { get; set; }
}

public static class GfdIconRepository
{
    private static          byte[]?      GfdFileContents { get; set; }
    private static          SKImage?     FontIconHandle  { get; set; }
    private static readonly SKColorSpace SkColorSpace = SKColorSpace.CreateSrgb();

    private static Dictionary<BitmapFontIcon, IDalamudTextureWrap>  IconCache  { get; } = [];
    private static Dictionary<BitmapFontIcon, GfdFileView.GfdEntry> EntryCache { get; } = [];

    internal static void Setup()
    {
        FontIconHandle  =   TextureLoader.LoadTexture("common/font/fonticon_ps5.tex")!;
        GfdFileContents ??= DalamudServices.DataManager.GetFile("common/font/gfdata.gfd")!.Data;
    }

    internal static void Dispose()
    {
        EntryCache.Clear();
        FontIconHandle?.Dispose();
        GfdFileContents = null;
    }

    public static unsafe GfdIcon GetIcon(BitmapFontIcon icon)
    {
        if (null == FontIconHandle || null == GfdFileContents) {
            throw new InvalidOperationException("GFD file or font icon texture not loaded.");
        }

        if (!EntryCache.TryGetValue(icon, out var entry)) {
            var fileView = new GfdFileView(new(Unsafe.AsPointer(ref GfdFileContents[0]), GfdFileContents.Length));

            if (!fileView.TryGetEntry((uint)icon, out entry)) {
                throw new ArgumentOutOfRangeException($"No GFD entry found for icon \"{icon}\".");
            }

            EntryCache[icon] = entry;
        }

        return new() { Texture = FontIconHandle, Uv0 = new Vector2(entry.Left, entry.Top + 170) * 2, Uv1 = new Vector2(entry.Left + entry.Width, entry.Top + entry.Height + 170) * 2, Size = new(entry.Width, entry.Height) };
    }

    public static IDalamudTextureWrap? GetIconWrap(BitmapFontIcon bitmapFontIcon)
    {
        if (IconCache.TryGetValue(bitmapFontIcon, out var c)) {
            return c;
        }

        GfdIcon icon = GetIcon(bitmapFontIcon);

        int width  = (int)icon.Size.X;
        int height = (int)icon.Size.Y;

        if (width < 1 || height < 1) return null;

        SKImageInfo info = new() {
            Width      = width,
            Height     = height,
            AlphaType  = SKAlphaType.Premul,
            ColorType  = SKColorType.Bgra8888,
            ColorSpace = SkColorSpace,
        };

        using SKSurface? surface = SKSurface.Create(info);

        if (surface == null) {
            DalamudServices.PluginLog.Warning($"Failed to create surface of size {width}x{height}.");
            return null;
        }

        SKRect src = new(icon.Uv0.X, icon.Uv0.Y, icon.Uv1.X, icon.Uv1.Y);
        SKRect dest = new(0, 0, width, height);

        using SKCanvas canvas = surface.Canvas;
        canvas.DrawImage(icon.Texture, src, dest);

        using SKImage  image  = surface.Snapshot();
        using SKPixmap pixmap = image.PeekPixels();

        byte[] data = new byte[pixmap.Width * pixmap.Height * 4];
        Marshal.Copy(pixmap.GetPixels(), data, 0, data.Length);

        var texture = DalamudServices.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(width, height), pixmap.GetPixelSpan());

        IconCache[bitmapFontIcon] = texture;

        return texture;
    }

    // From Kizer: https://github.com/Soreepeong/Dalamud/blob/feature/log-wordwrap/Dalamud/Interface/Spannables/Internal/GfdFileView.cs
    private readonly unsafe ref struct GfdFileView
    {
        private readonly ReadOnlySpan<byte> _bytes;
        private readonly bool               _directLookup;

        internal GfdFileView(ReadOnlySpan<byte> bytes)
        {
            _bytes = bytes;

            if (bytes.Length < sizeof(GfdHeader))
                throw new InvalidDataException($"Not enough space for a {nameof(GfdHeader)}");

            if (bytes.Length < sizeof(GfdHeader) + (Header.Count * sizeof(GfdEntry)))
                throw new InvalidDataException($"Not enough space for all the {nameof(GfdEntry)}");

            var entries = Entries;
            _directLookup = true;
            for (var i = 0; i < entries.Length && _directLookup; i++) _directLookup &= i + 1 == entries[i].Id;
        }

        /// <summary>Gets the header.</summary>
        private ref readonly GfdHeader Header => ref MemoryMarshal.AsRef<GfdHeader>(_bytes);

        /// <summary>Gets the entries.</summary>
        private ReadOnlySpan<GfdEntry> Entries => MemoryMarshal.Cast<byte, GfdEntry>(_bytes[sizeof(GfdHeader)..]);

        /// <summary>Attempts to get an entry.</summary>
        /// <param name="iconId">The icon ID.</param>
        /// <param name="entry">The entry.</param>
        /// <param name="followRedirect">Whether to follow redirects.</param>
        /// <returns><c>true</c> if found.</returns>
        public bool TryGetEntry(uint iconId, out GfdEntry entry, bool followRedirect = true)
        {
            if (iconId == 0) {
                entry = default;
                return false;
            }

            var entries = Entries;

            if (_directLookup) {
                if (iconId <= entries.Length) {
                    entry = entries[(int)(iconId - 1)];
                    return !entry.IsEmpty;
                }

                entry = default;
                return false;
            }

            var lo = 0;
            var hi = entries.Length;

            while (lo <= hi) {
                var i = lo + ((hi - lo) >> 1);

                if (entries[i].Id == iconId) {
                    if (followRedirect && entries[i].Redirect != 0) {
                        iconId = entries[i].Redirect;
                        lo     = 0;
                        hi     = entries.Length;
                        continue;
                    }

                    entry = entries[i];
                    return !entry.IsEmpty;
                }

                if (entries[i].Id < iconId)
                    lo = i + 1;
                else
                    hi = i - 1;
            }

            entry = default;
            return false;
        }

        /// <summary>Header of a .gfd file.</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct GfdHeader
        {
            /// <summary>Signature: "gftd0100".</summary>
            public fixed byte Signature[8];

            /// <summary>Number of entries.</summary>
            public int Count;

            /// <summary>Unused/unknown.</summary>
            public fixed byte Padding[4];
        }

        /// <summary>An entry of a .gfd file.</summary>
        [StructLayout(LayoutKind.Sequential, Size = 0x10)]
        internal struct GfdEntry
        {
            /// <summary>ID of the entry.</summary>
            public ushort Id;

            /// <summary>The left offset of the entry.</summary>
            public ushort Left;

            /// <summary>The top offset of the entry.</summary>
            public ushort Top;

            /// <summary>The width of the entry.</summary>
            public ushort Width;

            /// <summary>The height of the entry.</summary>
            public ushort Height;

            /// <summary>Unknown/unused.</summary>
            public ushort Unk0A;

            /// <summary>The redirected entry, maybe.</summary>
            public ushort Redirect;

            /// <summary>Unknown/unused.</summary>
            public ushort Unk0E;

            /// <summary>Gets a value indicating whether this entry is effectively empty.</summary>
            public bool IsEmpty => Width == 0 || Height == 0;
        }
    }
}

internal readonly struct GfdIconTexture(int width, int height, byte[] data)
{
    public int    Width  { get; } = width;
    public int    Height { get; } = height;
    public byte[] Data   { get; } = data;
}