using System.Numerics;
using System.Text;
using Chat2Ex.Code;
using Chat2Ex.Util;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;

namespace Chat2Ex.Native;

/// <summary>
///     Owns the native (KamiToolKit) chat windows and keeps them in sync with the plugin state.
/// </summary>
public sealed class NativeChatUi : IDisposable
{
    private readonly Plugin Plugin;
    private readonly Dictionary<int, NativeChatWindow> popouts = [];

    private NativeChatWindow? window;
    private NativeCommandHelp? commandHelp;
    private NativeInputPreview? inputPreview;
    private string previewInput = string.Empty;
    private Message? previewMessage;
    private bool failed;

    public NativeChatUi(Plugin plugin)
    {
        Plugin = plugin;

        Plugin.Commands.Register("/clearlog2", "Clear the Chat 2 chat log").Execute += ClearLog;
        Plugin.ClientState.Login += OnLogin;
    }

    /// <summary>
    ///     The chat window is created while the client is not logged in yet. Without refreshing the
    ///     activity time on login the window would be hidden by the inactivity timeout as soon as
    ///     the client logs in.
    /// </summary>
    private void OnLogin()
    {
        // The native windows are created while the client is not logged in, where the creation can
        // fail. Allow another attempt now that the client is logged in.
        failed = false;

        window?.MarkActivity();

        foreach (var popout in popouts.Values)
            popout.MarkActivity();
    }

    private void ClearLog(string command, string args) => Plugin.MessageManager.ClearAllTabs();

    public void Update()
    {
        if (failed)
            return;

        try
        {
            UpdateMainWindow();
            UpdatePopouts();
        }
        catch (Exception ex)
        {
            failed = true;
            Plugin.Log.Error(ex, "Native chat UI update failed, disabling the native chat window");

            try
            {
                window?.SetVisible(false);

                foreach (var popout in popouts.Values)
                    popout.SetVisible(false);
            }
            catch
            {
                // ignored
            }
        }
    }

    /// <summary>
    ///     Forces all native chat windows to rebuild their message list, e.g. after the channel
    ///     filters were changed in the settings.
    /// </summary>
    public void InvalidateAll()
    {
        window?.InvalidateRenderedMessages();

        foreach (var popout in popouts.Values)
            popout.InvalidateRenderedMessages();
    }

    public void SetHidden(bool hidden)
    {
        window?.SetVisible(!hidden);

        if (!hidden)
            return;

        commandHelp?.SetVisible(false);
        inputPreview?.SetVisible(false);
        previewInput = string.Empty;
    }

    /// <summary>
    ///     Shows the main chat window and focuses the chat input, used by Dalamud's main UI entry
    ///     point.
    /// </summary>
    public void OpenMainWindow()
    {
        // Activating the chat shows the window and focuses the input, the same as when the game
        // opens its own chat box.
        Plugin.ChatLog.Activate = true;
    }

    /// <summary>
    ///     Current position of the main chat window, or null while it has not been created yet.
    /// </summary>
    public Vector2? MainWindowPosition => window?.LastWindowPos;

    /// <summary>
    ///     Current size of the main chat window.
    /// </summary>
    public Vector2? MainWindowSize => window?.LastWindowSize;

    /// <summary>
    ///     Shows the slash command help popup next to the chat window.
    /// </summary>
    public void ShowCommandHelp(Lumina.Text.ReadOnly.ReadOnlySeString description)
    {
        if (window is null || Plugin.Config.CommandHelpSide is CommandHelpSide.None)
            return;

        if (!window.IsOpen)
            return;

        if (commandHelp is null)
        {
            try
            {
                commandHelp = new NativeCommandHelp(Plugin)
                {
                    InternalName = "Chat2Ex_CommandHelp",
                    Title = Plugin.PluginName,
                    Size = new Vector2(350.0f, 80.0f),
                    DepthLayer = 13,
                    DisableClose = true,
                    DisableFocusability = true,
                    DisableFocusOnShow = true,
                    DisableUnfocusedCloseOnEsc = true,
                    RespectCloseAll = false,
                };

                commandHelp.Open();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to create the native command help window");
                commandHelp = null;
                return;
            }
        }

        commandHelp.Show(description, MainWindowPosition ?? Vector2.Zero, MainWindowSize ?? new Vector2(560.0f, 320.0f));
    }

    /// <summary>
    ///     Hides the slash command help popup.
    /// </summary>
    public void HideCommandHelp() => commandHelp?.SetVisible(false);

    /// <summary>
    ///     Moves the main chat window, used by the settings window.
    /// </summary>
    public void MoveMainWindow(Vector2 position) => window?.SetWindowPosition(position);

    private void UpdateMainWindow()
    {
        try
        {
            UpdateMainWindowInternal();
        }
        catch (Exception ex)
        {
            // This runs inside the game's draw callback, an exception would terminate the process.
            Plugin.Log.Error(ex, "Chat2Ex: error while updating the chat window");
        }
    }

    private void UpdateMainWindowInternal()
    {
        if (window is null)
        {
            if (!Plugin.KamiToolKitReady)
                return;

            // The game resets its UI when the client logs in, addons that were created before the
            // login are torn down with it and stay invisible. The window is therefore created
            // after the login.
            if (!Plugin.ClientState.IsLoggedIn && Plugin.Config.HideWhenNotLoggedIn)
                return;

            try
            {
                window = CreateWindow(-1, "Chat2Ex_Main", ConfiguredMainWindowSize);
                window.Open();
                window.SetVisible(!Plugin.ChatLog.IsHidden);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to create the native chat window");
                failed = true;
            }

            return;
        }

        var hidden = Plugin.ChatLog.IsHidden;

        if (!hidden)
        {
            if (Plugin.ChatLog.Activate)
            {
                // Opening the chat counts as activity. The activation is consumed by the input
                // focus handling right after, so the window would be hidden again otherwise.
                window.MarkActivity();
            }
            else if (IsInactive(window))
            {
                hidden = true;
            }
        }

        window.SetVisible(!hidden);

        if (hidden)
        {
            HideCommandHelp();
            inputPreview?.SetVisible(false);
            previewInput = string.Empty;
            return;
        }

        window.SyncPendingTab();
        window.UpdateContent();
        window.SyncInputFocus();
        UpdateInputPreview();
    }

    /// <summary>
    ///     Rebuilds the input preview window when the typed text changes.
    /// </summary>
    private void UpdateInputPreview()
    {
        if (window is null || !window.IsOpen)
            return;

        var handler = Plugin.ChatLog;
        var input = handler.ChatInput;

        if (!handler.Activate || string.IsNullOrEmpty(input) || input.Length < Plugin.Config.PreviewMinimum)
        {
            inputPreview?.SetVisible(false);
            previewInput = string.Empty;
            previewMessage = null;
            return;
        }

        if (inputPreview is null)
        {
            try
            {
                inputPreview = new NativeInputPreview(Plugin)
                {
                    InternalName = "Chat2Ex_InputPreview",
                    Title = Plugin.PluginName,
                    Size = new Vector2(320.0f, 40.0f),
                    DepthLayer = 13,
                    DisableClose = true,
                    DisableFocusability = true,
                    DisableFocusOnShow = true,
                    DisableUnfocusedCloseOnEsc = true,
                    RespectCloseAll = false,
                };

                inputPreview.Open();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to create the native input preview window");
                inputPreview = null;
                return;
            }
        }

        if (input != previewInput || previewMessage is null)
        {
            previewInput = input;
            previewMessage = BuildPreviewMessage(input);
        }

        if (previewMessage is null || (Plugin.Config.OnlyPreviewIf && previewMessage.Content.Count <= 1))
        {
            inputPreview.SetVisible(false);
            return;
        }

        inputPreview.Show(previewMessage, MainWindowPosition ?? Vector2.Zero, MainWindowSize ?? new Vector2(560.0f, 320.0f));
    }

    private static Message BuildPreviewMessage(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input.Trim());
        AutoTranslate.ReplaceWithPayload(ref bytes);

        var chunks = ChunkUtil.ToChunks(SeString.Parse(bytes), ChunkSource.Content, ChatType.Say).ToList();
        var message = Message.FakeMessage(chunks, new ChatCode(XivChatType.Say, 0, 0));
        message.DecodeTextParam();

        return message;
    }

    private void UpdatePopouts()
    {
        if (window is null)
            return;

        var tabs = Plugin.Config.Tabs;

        foreach (var index in popouts.Keys.ToArray())
        {
            if (index < tabs.Count && tabs[index].PopOut)
                continue;

            CloseWindow(popouts[index]);
            popouts.Remove(index);
        }

        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            if (!tab.PopOut)
                continue;

            if (!popouts.TryGetValue(i, out var popout))
            {
                // See UpdateMainWindowInternal, windows must not be created before the login.
                if (!Plugin.ClientState.IsLoggedIn && Plugin.Config.HideWhenNotLoggedIn)
                    continue;

                try
                {
                    popout = CreateWindow(i, $"Chat2Ex_Popout{i}", new Vector2(400.0f, 300.0f));
                    popout.Open();
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Failed to create the native popout window");
                    continue;
                }

                popouts[i] = popout;
            }

            var hidden = tab.IndependentHide
                ? HideStateHelper.HideStateCheck(popout, tab.HideInBattle, tab.HideDuringCutscenes, tab.HideWhenNotLoggedIn, false)
                : Plugin.ChatLog.IsHidden;

            if (!hidden)
            {
                if (Plugin.ChatLog.Activate)
                    popout.MarkActivity();
                else if (IsInactive(popout))
                    hidden = true;
            }

            popout.SetVisible(!hidden);

            if (hidden)
                continue;

            try
            {
                popout.UpdateContent();
                popout.SyncInputFocus();
            }
            catch (Exception ex)
            {
                // This runs inside the game's draw callback, an exception would terminate the process.
                Plugin.Log.Error(ex, "Chat2Ex: error while updating a popout window");
            }
        }
    }

    /// <summary>
    ///     Size of the main chat window, restored from the configuration.
    /// </summary>
    private Vector2 ConfiguredMainWindowSize
    {
        get
        {
            var size = Plugin.Config.ChatWindowSize;
            if (size.X < 100.0f || size.Y < 100.0f)
                return new Vector2(560.0f, 320.0f);

            return size;
        }
    }

    private NativeChatWindow CreateWindow(int tabIndex, string internalName, Vector2 size) => new(Plugin, tabIndex)
    {
        InternalName = internalName,
        Title = Plugin.PluginName,
        Size = size,
        DepthLayer = 3,
        DisableClose = true,
        DisableFocusOnShow = true,
        DisableUnfocusedCloseOnEsc = true,
        RespectCloseAll = false,
    };

    /// <summary>
    ///     Mirrors the ImGui chat log's "hide when inactive" behavior, which previously lived in its DrawConditions.
    /// </summary>
    private bool IsInactive(NativeChatWindow target)
    {
        if (!Plugin.Config.HideWhenInactive)
            return false;

        if (!Plugin.Config.InactivityHideActiveDuringBattle && Plugin.InBattle)
            return false;

        // Opening the chat counts as activity, otherwise the window would be hidden again in the
        // next frame, because the activation is consumed by the input focus handling.
        if (Plugin.ChatLog.Activate)
        {
            target.MarkActivity();
            return false;
        }

        // Tabs that received a message keep the window visible as well.
        var lastActivity = target.LastActivityTime;
        var currentTab = Plugin.CurrentTab;

        foreach (var tab in Plugin.Config.Tabs)
        {
            if (tab.PopOut)
                continue;

            if (!tab.UnhideOnActivity && tab != currentTab)
                continue;

            lastActivity = Math.Max(lastActivity, tab.LastActivity);
        }

        return Environment.TickCount64 - lastActivity > 1000L * Plugin.Config.InactivityHideTimeout;
    }

    private void CloseWindow(KamiToolKit.BaseTypes.NativeAddon target)
    {
        try
        {
            // Dispose (not Close) so the addon is removed from KamiToolKit's tracking list, which
            // otherwise produces "was not disposed properly" warnings on plugin unload.
            target.Dispose();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to dispose a native chat window");
        }
    }

    public void Dispose()
    {
        Plugin.Commands.Register("/clearlog2").Execute -= ClearLog;
        Plugin.ClientState.Login -= OnLogin;

        var main = window;
        window = null;

        var help = commandHelp;
        commandHelp = null;

        var preview = inputPreview;
        inputPreview = null;

        var popoutWindows = popouts.Values.ToArray();
        popouts.Clear();

        void CloseAll()
        {
            foreach (var popout in popoutWindows)
                CloseWindow(popout);

            if (preview is not null)
                CloseWindow(preview);

            if (help is not null)
                CloseWindow(help);

            if (main is not null)
                CloseWindow(main);
        }

        if (ThreadSafety.IsMainThread)
            CloseAll();
        else
            Plugin.Framework.RunOnFrameworkThread(CloseAll);
    }
}
