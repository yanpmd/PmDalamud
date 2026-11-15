namespace Chat2Ex.Ui.Handler;

/// <summary>
///     Lightweight bridge that lets the original ImGui handlers (chunk rendering and the ImGui
///     context menu) talk to the native chat window without owning a second copy of the input state.
///     Every stateful member is forwarded to <see cref="ChatState" />, which stays the single source
///     of truth for the native input node, the keybind hooks and the typing IPC.
/// </summary>
public sealed class InputHandler
{
    private readonly Plugin plugin;

    public Plugin Plugin => plugin;
    public string InputHandlerId => string.Empty;

    public ChunkHandler ChunkHandler { get; }
    public PayloadHandler PayloadHandler { get; }
    public IChatWindow MainWindow => plugin.ChatLog;

    public string ChatInput
    {
        get => plugin.ChatLog.ChatInput;
        set => plugin.ChatLog.ChatInput = value;
    }

    public bool Activate
    {
        get => plugin.ChatLog.Activate;
        set => plugin.ChatLog.Activate = value;
    }

    public int CursorPos
    {
        get => plugin.ChatLog.CursorPos;
        set => plugin.ChatLog.CursorPos = value;
    }

    public InputHandler(Plugin plugin)
    {
        this.plugin = plugin;
        ChunkHandler = new ChunkHandler(plugin);
        PayloadHandler = new PayloadHandler(this);
    }
}
