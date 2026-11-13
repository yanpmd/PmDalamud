using System.Numerics;
using KamiToolKit.BaseTypes;

namespace Chat2Ex.Native;

/// <summary>
///     Owns the native tool windows (DB viewer, debugger, SeString debugger) and their commands.
/// </summary>
public sealed class NativeToolWindows : IDisposable
{
    private readonly Plugin Plugin;

    private NativeDbViewer? dbViewer;
    private NativeDebuggerWindow? debugger;
    private NativeSeStringDebugger? seStringDebugger;

    public NativeToolWindows(Plugin plugin)
    {
        Plugin = plugin;

        Plugin.Commands.Register("/chat2Viewer", "Get access to your message history, with simple filter options.", true).Execute += ToggleDbViewer;
        Plugin.Commands.Register("/chat2Debugger", showInHelp: false).Execute += ToggleDebugger;
#if DEBUG
        Plugin.Commands.Register("/chat2SeString", showInHelp: false).Execute += ToggleSeStringDebugger;
#endif
    }

    private void ToggleDbViewer(string command, string args)
    {
        if (!Plugin.KamiToolKitReady)
            return;

        if (dbViewer is null)
        {
            try
            {
                dbViewer = new NativeDbViewer(Plugin)
                {
                    InternalName = "Chat2Ex_DbViewer",
                    Title = "DBViewer",
                    Size = new Vector2(700.0f, 600.0f),
                    DepthLayer = 13,
                    RespectCloseAll = false,
                };

                dbViewer.Open();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to create the native DB viewer");
                dbViewer = null;
            }

            return;
        }

        dbViewer.SetVisible(!dbViewer.IsOpen);
    }

    private void ToggleDebugger(string command, string args)
    {
        if (!Plugin.KamiToolKitReady)
            return;

        if (debugger is null)
        {
            try
            {
                debugger = new NativeDebuggerWindow(Plugin)
                {
                    InternalName = "Chat2Ex_Debugger",
                    Title = "Debugger",
                    Size = new Vector2(560.0f, 520.0f),
                    DepthLayer = 13,
                    RespectCloseAll = false,
                };

                debugger.Open();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to create the native debugger window");
                debugger = null;
            }

            return;
        }

        debugger.SetVisible(!debugger.IsOpen);
    }

#if DEBUG
    private void ToggleSeStringDebugger(string command, string args)
    {
        if (!Plugin.KamiToolKitReady)
            return;

        if (seStringDebugger is null)
        {
            try
            {
                seStringDebugger = new NativeSeStringDebugger(Plugin)
                {
                    InternalName = "Chat2Ex_SeStringDebugger",
                    Title = "SeString Debugger",
                    Size = new Vector2(560.0f, 520.0f),
                    DepthLayer = 13,
                    RespectCloseAll = false,
                };

                seStringDebugger.Open();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to create the native SeString debugger");
                seStringDebugger = null;
            }

            return;
        }

        seStringDebugger.SetVisible(!seStringDebugger.IsOpen);
    }
#endif

    public void Dispose()
    {
        Plugin.Commands.Register("/chat2Viewer").Execute -= ToggleDbViewer;
        Plugin.Commands.Register("/chat2Debugger").Execute -= ToggleDebugger;
#if DEBUG
        Plugin.Commands.Register("/chat2SeString").Execute -= ToggleSeStringDebugger;
#endif

        CloseWindow(dbViewer);
        CloseWindow(debugger);
        CloseWindow(seStringDebugger);

        dbViewer = null;
        debugger = null;
        seStringDebugger = null;
    }

    private void CloseWindow(NativeAddon? addon)
    {
        if (addon is null)
            return;

        try
        {
            // Dispose (not Close) so KamiToolKit stops tracking the addon.
            addon.Dispose();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to dispose a native tool window");
        }
    }
}
