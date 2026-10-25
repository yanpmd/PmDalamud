using Dalamud.Interface;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Una.Drawing.Debugger;
using Una.Drawing.Font;
using Una.Drawing.NodeParser;
using Una.Drawing.Templating.StyleParser;
using Una.Drawing.Texture;

namespace Una.Drawing;

public class DrawingLib
{
    public static bool ShowDebugWindow { get; set; }

    /// <summary>
    /// Set up the drawing library. Make sure to call this method in your
    /// plugin before using any of the drawing library's features.
    /// </summary>
    public static async void Setup(IDalamudPluginInterface pluginInterface, bool downloadGameGlyphs = true)
    {
        pluginInterface.Create<DalamudServices>();
        DalamudServices.PluginInterface = pluginInterface;
        DalamudServices.UiBuilder       = pluginInterface.UiBuilder;

        ElementRegistry.Register<Node>();

        DalamudServices.CommandManager.AddHandler("/una-drawing",
            new(OnChatCommand) { HelpMessage = "Una.Drawing Commands", ShowInHelp = false }
        );

        pluginInterface.UiBuilder.Draw += OnDraw;

        if (downloadGameGlyphs) {
            try {
                await GameGlyphProvider.DownloadGameGlyphs();
                FontRegistry.SetupGlyphFont();
            } catch (Exception e) {
                DalamudServices.PluginLog.Warning($"Failed to prepare game glyph font: {e.Message}");
            }
        }

#if DEBUG
        DebugLogger.Writer = DalamudServices.PluginLog;
#endif

        // Font file names shipped with Dalamud change between asset versions
        // (NotoSansKR was replaced by NotoSansCJKsc in newer builds), so pick
        // the first candidate that exists and fall back to a system font. This
        // must never throw: Setup is async void, and an exception here takes
        // down the whole game.
        SetFontOrFallback(
            pluginInterface,
            0,
            ["NotoSansKR-Regular.otf", "NotoSansCJKsc-Medium.otf", "NotoSansCJKjp-Regular.otf", "NotoSansCJKsc-Regular.otf"],
            "Microsoft YaHei"
        );

        SetFontOrFallback(
            pluginInterface,
            1,
            ["Inconsolata-Regular.ttf", "Inconsolata-Regular.otf"],
            "Consolas"
        );

        SetFontOrFallback(pluginInterface, 2, ["FontAwesomeFreeSolid.otf"], "Arial");

        FontRegistry.SetNativeFontFamily(3, "Arial");

        if (GameGlyphProvider.GlyphsFile.Exists) {
            FontRegistry.SetNativeFontFamily(4, GameGlyphProvider.GlyphsFile);
        }

        GfdIconRepository.Setup();
        Renderer.Setup();

        UdtLoader.RegisterAttributeValueParser(new DefaultAttributeValueParser());
        UdtLoader.RegisterDirectiveParser(new SeStringDirectiveParser());
    }

    /// <summary>
    /// Registers a font from the first available Dalamud asset file, falling
    /// back to a system font family when none of the candidates exist.
    /// </summary>
    private static void SetFontOrFallback(
        IDalamudPluginInterface pluginInterface,
        uint                    id,
        string[]                candidates,
        string                  fallbackFamily
    )
    {
        try {
            foreach (string name in candidates) {
                var file = new FileInfo(Path.Combine(pluginInterface.DalamudAssetDirectory.FullName, "UIRes", name));

                if (!file.Exists) continue;

                FontRegistry.SetNativeFontFamily(id, file, 0);

                return;
            }

            DalamudServices.PluginLog.Warning($"Font for id {id} not found in Dalamud assets, using '{fallbackFamily}'.");

            FontRegistry.SetNativeFontFamily(id, fallbackFamily, 0);
        } catch (Exception e) {
            DalamudServices.PluginLog.Warning($"Failed to register font '{id}': {e.Message}");
        }
    }

    /// <summary>
    /// Disposes of the allocated resources in the drawing library. Make sure
    /// to call invoke this in your plugin's Dispose method.
    /// </summary>
    public static void Dispose()
    {
        DebugLogger.Log($"Shutting down {nameof(DrawingLib)}...");

        DalamudServices.PluginInterface.UiBuilder.Draw -= OnDraw;

        Renderer.Dispose();
        FontRegistry.Dispose();
        GfdIconRepository.Dispose();
        TextureLoader.Dispose();
        MouseCursor.Dispose();
        StylesheetRegistry.Dispose();
        ElementRegistry.Dispose();
        StyleAttributeParser.Dispose();
        NodeAttributeParser.Dispose();
        QuerySelectorParser.Dispose();
        NodeDebugger.Dispose();
        UdtLoader.Dispose();

        // Force the GC to run to clean up any remaining resources.
        if (!DalamudServices.Framework.IsFrameworkUnloading) {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private static void OnDraw()
    {
        MouseCursor.Update();

        if (ShowDebugWindow) {
            NodeDebugger.Render();
        }
    }

    private static void OnChatCommand(string command, string args)
    {
        if (command != "/una-drawing") return;

        if (args == String.Empty) {
            Print("Available commands: debug, bounds.");
            return;
        }

        switch (args.ToLower()) {
            case "debug":
                ShowDebugWindow = !ShowDebugWindow;
                break;
            case "bounds":
                Node.DrawDebugBoundingBoxes = !Node.DrawDebugBoundingBoxes;
                break;
            default:
                Print($"Unknown command: {args}. Available commands: debug, bounds.");
                break;
        }
    }

    private static void Print(string msg)
    {
        if (DalamudServices.ClientState.IsLoggedIn) {
            DalamudServices.ChatGui.Print(msg);
        } else {
            DalamudServices.PluginLog.Info(msg);
        }
    }
}

internal class DalamudServices
{
    [PluginService] public static IDataManager                 DataManager                 { get; set; } = null!;
    [PluginService] public static ITextureProvider             TextureProvider             { get; set; } = null!;
    [PluginService] public static ITextureSubstitutionProvider TextureSubstitutionProvider { get; set; } = null!;
    [PluginService] public static ITextureReadbackProvider     TextureReadbackProvider     { get; set; } = null!;
    [PluginService] public static IPluginLog                   PluginLog                   { get; set; } = null!;
    [PluginService] public static ICommandManager              CommandManager              { get; set; } = null!;
    [PluginService] public static IChatGui                     ChatGui                     { get; set; } = null!;
    [PluginService] public static IClientState                 ClientState                 { get; set; } = null!;
    [PluginService] public static IFramework                   Framework                   { get; set; } = null!;

    public static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    public static IUiBuilder              UiBuilder       { get; set; } = null!;
}