using System.Numerics;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace Chat2Ex.Native;

/// <summary>
///     Native replacement for the ImGui debugger window.
/// </summary>
public sealed class NativeDebuggerWindow : NativeAddon
{
    private readonly Plugin Plugin;

    private ScrollingNode<VerticalListNode>? scroll;
    private TextButtonNode? refreshButton;
    private TextButtonNode? copyButton;

    private long lastRefresh;
    private string cachedText = string.Empty;

    public NativeDebuggerWindow(Plugin plugin) => Plugin = plugin;

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        var start = ContentStartPosition;
        var size = ContentSize;

        scroll = new ScrollingNode<VerticalListNode>
        {
            Position = start,
            Size = new Vector2(size.X, size.Y - 32.0f),
            AutoHideScrollBar = true,
        };
        scroll.ContentNode.FitWidth = true;
        scroll.ContentNode.FitContents = true;
        scroll.AttachNode(this);
        scroll.RecalculateSizes();

        var footerY = start.Y + size.Y - 28.0f;

        refreshButton = new TextButtonNode
        {
            Position = new Vector2(start.X, footerY),
            Size = new Vector2(140.0f, 28.0f),
            String = "Refresh",
            OnClick = () => Refresh(true),
        };
        refreshButton.AttachNode(this);

        copyButton = new TextButtonNode
        {
            Position = new Vector2(start.X + 148.0f, footerY),
            Size = new Vector2(180.0f, 28.0f),
            String = "Copy to clipboard",
            OnClick = () => Dalamud.Bindings.ImGui.ImGui.SetClipboardText(cachedText),
        };
        copyButton.AttachNode(this);

        Refresh(true);
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        scroll = null;
        refreshButton = null;
        copyButton = null;

        base.OnFinalize(addon);
    }

    protected override unsafe void OnDraw(AtkUnitBase* addon)
    {
        if (lastRefresh + 1000 < Environment.TickCount64)
            Refresh(false);

        base.OnDraw(addon);
    }

    public unsafe void SetVisible(bool visible)
    {
        if (InternalAddon is null || InternalAddon->IsVisible == visible)
            return;

        InternalAddon->IsVisible = visible;
        RootNode.IsVisible = visible;
    }

    private void Refresh(bool force)
    {
        if (!force && scroll is null)
            return;

        var builder = new StringBuilder();
        builder.AppendLine($"IsHidden: {Plugin.ChatLog.IsHidden}");
        builder.AppendLine($"HideState: {Plugin.ChatLog.CurrentHideState}");
        builder.AppendLine($"Activate: {Plugin.ChatLog.Activate} / InputFocused: {Plugin.ChatLog.InputFocused}");
        builder.AppendLine($"CursorPos: {Plugin.ChatLog.CursorPos}");
        builder.AppendLine($"ChatInput: '{Plugin.ChatLog.ChatInput}'");
        builder.AppendLine($"TellSpecial: {Plugin.ChatLog.TellSpecial}");
        builder.AppendLine();
        builder.AppendLine($"LastTab: {Plugin.LastTab} / WantedTab: {Plugin.WantedTab}");

        var tab = Plugin.CurrentTab;
        builder.AppendLine($"Current tab: {tab.Name}");
        builder.AppendLine($"Channel: {tab.CurrentChannel.Channel}");
        builder.AppendLine($"Temp channel: {tab.CurrentChannel.TempChannel} ({tab.CurrentChannel.UseTempChannel})");
        builder.AppendLine($"Unread: {tab.Unread}");

        using (var messages = tab.Messages.GetReadOnly())
            builder.AppendLine($"Messages in tab: {messages.Count}");

        builder.AppendLine($"Messages in database: {Plugin.MessageManager.Store.MessageCount()}");

        unsafe
        {
            var unitManager = RaptureAtkModule.Instance();
            builder.AppendLine($"Loaded addons: {(unitManager is null ? 0 : unitManager->RaptureAtkUnitManager.AllLoadedUnitsList.Count)}");
        }

        cachedText = builder.ToString();
        lastRefresh = Environment.TickCount64;

        if (scroll is null)
            return;

        var list = scroll.ContentNode;
        list.Clear();

        var width = scroll.ContentNode.Width;

        foreach (var line in cachedText.Split('\n'))
        {
            list.AddNode(new TextNode
            {
                Size = new Vector2(width, 16.0f),
                FontSize = 12,
                LineSpacing = 15,
                String = line.TrimEnd('\r'),
            });
        }

        list.RecalculateLayout();
        scroll.RecalculateSizes();
    }
}
