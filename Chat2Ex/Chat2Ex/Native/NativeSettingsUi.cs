using System.Numerics;
using Chat2Ex.Resources;

namespace Chat2Ex.Native;

/// <summary>
///     Owns the native settings window.
/// </summary>
public sealed class NativeSettingsUi : IDisposable
{
    private readonly Plugin Plugin;

    private NativeSettingsWindow? window;
    private bool pendingOpen;

    public NativeSettingsUi(Plugin plugin)
    {
        Plugin = plugin;

        Plugin.Commands.Register("/chat2", "Perform various actions with Chat 2.").Execute += Command;
        Plugin.Interface.UiBuilder.OpenConfigUi += OpenFromDalamud;
    }

    private void Command(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
            Toggle();
    }

    private void OpenFromDalamud() => Toggle();

    public void Update()
    {
        if (pendingOpen && Plugin.KamiToolKitReady)
        {
            pendingOpen = false;
            Open();
        }
    }

    public void Toggle()
    {
        if (window is not null)
        {
            Close();
            return;
        }

        if (!Plugin.KamiToolKitReady)
        {
            pendingOpen = true;
            return;
        }

        Open();
    }

    public void Open()
    {
        if (window is not null || !Plugin.KamiToolKitReady)
            return;

        try
        {
            window = new NativeSettingsWindow(Plugin, OnWindowClosed)
            {
                InternalName = "Chat2Ex_Settings",
                Title = string.Format(Language.Settings_Title, Plugin.PluginName),
                Size = new Vector2(700.0f, 560.0f),
                DepthLayer = 13,
                DisableClose = true,
                RespectCloseAll = false,
            };

            window.Open();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to open the native settings window");
            window = null;
        }
    }

    private void OnWindowClosed() => window = null;

    public void Close()
    {
        var target = window;
        window = null;

        if (target is null)
            return;

        try
        {
            // Dispose (not Close) so KamiToolKit stops tracking the addon.
            target.Dispose();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to dispose the native settings window");
        }
    }

    public void Dispose()
    {
        Plugin.Commands.Register("/chat2").Execute -= Command;
        Plugin.Interface.UiBuilder.OpenConfigUi -= OpenFromDalamud;

        Close();
    }
}
