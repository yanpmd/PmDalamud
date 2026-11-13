using System.Numerics;
using Chat2Ex.Code;
using Chat2Ex.GameFunctions;
using Chat2Ex.GameFunctions.Types;
using Chat2Ex.Util;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Chat2Ex;

/// <summary>
///     Holds the chat state that used to live in the ImGui chat log window: hide state, the current
///     input, and the activation logic driven by the game's chat hooks.
/// </summary>
public sealed class ChatState : IChatWindow
{
    public const uint ChatOpenSfx = 35u;
    public const uint ChatCloseSfx = 3u;

    private readonly Plugin Plugin;

    public ChatState(Plugin plugin) => Plugin = plugin;

    public bool IsHidden { get; set; }

    public Vector2 LastWindowPos { get; set; }
    public Vector2 LastWindowSize { get; set; }
    public HideState CurrentHideState { get; set; }

    /// <summary>
    ///     Set for Eureka/Bozja/Zadnor tells that go through the game's command path.
    /// </summary>
    public bool TellSpecial;

    public string ChatInput = string.Empty;

    public bool Activate;
    public bool InputFocused;
    public bool PlayedClosingSound = true;

    public int CursorPos;
    public int ActivatePos = -1;

    /// <summary>
    ///     Channel name chunks, kept for the web interface's current channel display.
    /// </summary>
    public Chunk[] PreviousChannel = [];

    private InputChannel? displayedChannel;

    /// <summary>
    ///     Rebuilds <see cref="PreviousChannel" /> when the active input channel changed.
    /// </summary>
    public void UpdateChannelDisplay()
    {
        var current = Plugin.CurrentTab.CurrentChannel;
        var channel = current.UseTempChannel ? current.TempChannel : current.Channel;

        if (displayedChannel == channel)
            return;

        displayedChannel = channel;

        var name = channel.ToChatType().Name();
        var seString = new SeStringBuilder().AddText(name).Build();
        PreviousChannel = ChunkUtil.ToChunks(seString, ChunkSource.None, null).ToArray();
    }

    /// <summary>
    ///     Channels that can currently be switched to, for the web interface.
    /// </summary>
    public Dictionary<string, InputChannel> GetValidChannels()
    {
        var channels = new Dictionary<string, InputChannel>();

        foreach (var channel in Enum.GetValues<InputChannel>())
        {
            if (!Chat.IsChannelOrExistingLinkshell(channel))
                continue;

            channels[channel.ToChatType().Name()] = channel;
        }

        return channels;
    }

    public long FrameTime;
    public long LastActivityTime = Environment.TickCount64;

    public void BeginFrame() => FrameTime = Environment.TickCount64;

    public void FinalizeFrame()
    {
    }

    /// <summary>
    ///     Handles activation coming from the game's chat hooks and keybinds.
    /// </summary>
    public unsafe void Activated(ChatActivatedArgs args)
    {
        TellSpecial = args.TellSpecial;

        Activate = true;
        PlayedClosingSound = false;

        if (Plugin.Config.PlaySounds)
            UIGlobals.PlaySoundEffect(ChatOpenSfx);

        // Don't set the channel or text content when activating a disabled tab.
        if (Plugin.CurrentTab.InputDisabled)
        {
            PlayedClosingSound = true;
            return;
        }

        if (args.AddIfNotPresent != null && !ChatInput.Contains(args.AddIfNotPresent))
        {
            if (args.AddIfNotPresent.StartsWith('/'))
                ChatInput = args.AddIfNotPresent;
            else
                ChatInput += args.AddIfNotPresent;
        }

        if (args.Input != null)
        {
            if (args.Input.StartsWith('/'))
                ChatInput = args.Input;
            else
                ChatInput += args.Input;
        }

        var (info, reason, target) = (args.ChannelSwitchInfo, args.TellReason, args.TellTarget);
        if (info.Channel != null)
        {
            var targetChannel = info.Channel;
            if (info.Channel is InputChannel.Tell)
            {
                if (info.Rotate != RotateMode.None)
                {
                    var idx = Plugin.CurrentTab.CurrentChannel.TempChannel != InputChannel.Tell
                        ? 0
                        : info.Rotate == RotateMode.Reverse
                            ? -1
                            : 1;

                    var tellInfo = Plugin.Functions.Chat.GetTellHistoryInfo(idx);
                    if (tellInfo != null && reason != null)
                        Plugin.CurrentTab.CurrentChannel.TempTellTarget =
                            new TellTarget(tellInfo.Name, (ushort)tellInfo.World, tellInfo.ContentId, reason.Value);
                }
                else
                {
                    Plugin.CurrentTab.CurrentChannel.TellTarget = null;
                    if (target != null)
                    {
                        if (info.Permanent)
                        {
                            Plugin.CurrentTab.CurrentChannel.TellTarget = target;
                        }
                        else
                        {
                            Plugin.CurrentTab.CurrentChannel.UseTempChannel = true;
                            Plugin.CurrentTab.CurrentChannel.TempTellTarget = target;
                        }
                    }
                }
            }
            else
            {
                Plugin.CurrentTab.CurrentChannel.TellTarget = null;
            }

            if (info.Channel is InputChannel.Linkshell1 or InputChannel.CrossLinkshell1 && info.Rotate != RotateMode.None)
            {
                var module = UIModule.Instance();

                if (info.Permanent)
                {
                    if (info.Channel == InputChannel.Linkshell1)
                    {
                        Chat.RotateLinkshellHistory(info.Rotate);
                        targetChannel = info.Channel + (uint)module->LinkshellCycle;
                    }
                    else
                    {
                        Chat.RotateCrossLinkshellHistory(info.Rotate);
                        targetChannel = info.Channel + (uint)module->CrossWorldLinkshellCycle;
                    }
                }
                else
                {
                    targetChannel = Chat.ResolveTempInputChannel(Plugin.CurrentTab.CurrentChannel.TempChannel, info.Channel.Value, info.Rotate);
                }
            }

            if (targetChannel == null || !Chat.IsChannelOrExistingLinkshell(targetChannel.Value))
            {
                Plugin.Log.Warning($"Channel was set to an invalid value '{targetChannel}', ignoring");
                return;
            }

            if (info.Permanent)
                Plugin.Functions.Chat.SetChannelWithExtraChat(targetChannel);
            else
            {
                Plugin.CurrentTab.CurrentChannel.UseTempChannel = true;
                Plugin.CurrentTab.CurrentChannel.TempChannel = targetChannel.Value;
            }
        }

        if (info.Text != null && ChatInput.Length == 0)
            ChatInput = info.Text;
    }

    public void ChangeTab(int index)
    {
        Plugin.WantedTab = index;
        LastActivityTime = FrameTime;
    }

    public void ChangeTabDelta(int offset)
    {
        var tabCount = Plugin.Config.Tabs.Count;
        if (tabCount == 0)
            return;

        var newIndex = (Plugin.LastTab + offset) % tabCount;
        while (newIndex < 0)
            newIndex += tabCount;

        ChangeTab(newIndex);
    }

    public void Dispose()
    {
    }
}
