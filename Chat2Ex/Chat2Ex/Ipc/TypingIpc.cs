using Chat2Ex.Code;
using Dalamud.Plugin.Ipc;

namespace Chat2Ex.Ipc;

using ChatInputState = (bool InputVisible, bool InputFocused, bool HasText, bool IsTyping, int TextLength, ChatType ChannelType);

public sealed class TypingIpc : IDisposable
{
    private Plugin Plugin { get; }

    private ICallGateProvider<ChatInputState> StateQueryGate { get; }
    private ICallGateProvider<ChatInputState, object?> StateChangedGate { get; }

    private ChatInputState LastState;
    private bool HasState;

    public TypingIpc(Plugin plugin)
    {
        Plugin = plugin;

        StateQueryGate = Plugin.Interface.GetIpcProvider<ChatInputState>("Chat2Ex.GetChatInputState");
        StateChangedGate = Plugin.Interface.GetIpcProvider<ChatInputState, object?>("Chat2Ex.ChatInputStateChanged");

        StateQueryGate.RegisterFunc(GetState);
    }

    private ChatInputState BuildState()
    {
        var log = Plugin.ChatLog;

        var usedChannel = Plugin.CurrentTab.CurrentChannel;
        var inputChannel = usedChannel.UseTempChannel ? usedChannel.TempChannel : usedChannel.Channel;
        var channelType = inputChannel.ToChatType();

        return (InputVisible: !log.IsHidden,
            log.InputFocused,
            HasText: log.ChatInput.Length > 0,
            IsTyping: log is { InputFocused: true, ChatInput.Length: > 0 },
            TextLength: log.ChatInput.Length,
            ChannelType: channelType);
    }

    private ChatInputState GetState()
        => BuildState();

    public void Update()
    {
        var state = BuildState();
        if (HasState && state.Equals(LastState))
            return;

        HasState = true;
        LastState = state;
        StateChangedGate.SendMessage(state);
    }

    public void Dispose()
    {
        StateQueryGate.UnregisterFunc();
    }
}
