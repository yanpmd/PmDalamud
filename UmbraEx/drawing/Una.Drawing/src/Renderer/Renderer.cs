using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using System.Linq;
using System.Reflection;
using Una.Drawing.Generator;

namespace Una.Drawing;

internal static class Renderer
{
    private static List<IGenerator> _generators = [];

    private static nint _pixelDataPtr;

    private static readonly SKColorSpace SkColorSpace = SKColorSpace.CreateSrgb();

    internal static void Setup()
    {
        // Collect generators.
        List<Type> generatorTypes = Assembly
                                   .GetExecutingAssembly()
                                   .GetTypes()
                                   .Where(t => t.IsClass && t.IsAssignableTo(typeof(IGenerator)))
                                   .ToList();

        _generators = generatorTypes
                     .Select(t => (IGenerator)Activator.CreateInstance(t)!)
                     .OrderBy(g => g.RenderOrder)
                     .ToList();

        Tuple<nint> handle = DalamudServices.PluginInterface.GetOrCreateData(
            "Una.Drawing.Framebuffer",
            () => {
                var data   = new byte[8192 * 8192 * 4];
                var handle = GCHandle.Alloc(data, GCHandleType.Pinned);

                DebugLogger.Log($"Creating framebuffer of size {data.Length} bytes.");

                return Tuple.Create(handle.AddrOfPinnedObject());
            }
        );

        _pixelDataPtr = handle.Item1;
    }

    internal static void Dispose()
    {
        SkColorSpace.Dispose();
    }

    /// <summary>
    /// Creates a texture for the given node.
    /// </summary>
    internal static IDalamudTextureWrap? CreateTexture(Node node, Vector2? padding)
    {
        if (node.Width == 0 || node.Height == 0) return null;
        if (node.Width > 8192 || node.Height > 8192) return null;

        padding ??= Vector2.Zero;

        SKImageInfo info = new(
            (int)(node.Width + padding.Value.X),
            (int)(node.Height + padding.Value.Y),
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            SkColorSpace
        );

        using var pixmap  = new SKPixmap(info, _pixelDataPtr);
        using var surface = SKSurface.Create(pixmap);

        surface.Canvas.Clear();

        bool    hasDrawn = false;
        Vector2 origin   = new(padding.Value.X / 2f, padding.Value.Y / 2f);

        foreach (IGenerator generator in _generators) {
            try {
                if (generator.Generate(surface.Canvas, node, origin)) {
                    hasDrawn = true;
                }
            } catch (Exception e) {
                DalamudServices.PluginLog.Error($"{e.Message}\n{e.StackTrace}");
            }
        }

        if (!hasDrawn) return null;

        try {
            return DalamudServices.TextureProvider.CreateFromRaw(
                RawImageSpecification.Rgba32(
                    (int)(node.Width + padding.Value.X),
                    (int)(node.Height + padding.Value.Y)
                ),
                pixmap.GetPixelSpan(),
                node.ToString()
            );
        } catch (Exception e) {
            DalamudServices.PluginLog.Error($"{e.Message}\n{e.StackTrace}");

            // If the texture creation fails, we can just return null to prevent
            // crashes during the drawing process.
            return null;
        }
    }
}