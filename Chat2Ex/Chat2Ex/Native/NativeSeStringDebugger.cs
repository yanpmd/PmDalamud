using System.Numerics;
using System.Text;
using Chat2Ex.Util;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace Chat2Ex.Native;

/// <summary>
///     Native replacement for the ImGui SeString debugger: dumps the last chat message as macro
///     string plus its payloads.
/// </summary>
public sealed class NativeSeStringDebugger : NativeAddon
{
    private readonly Plugin Plugin;

    private ScrollingNode<VerticalListNode>? scroll;

    private SeString? renderedSender;
    private SeString? renderedContent;

    public NativeSeStringDebugger(Plugin plugin) => Plugin = plugin;

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        scroll = new ScrollingNode<VerticalListNode>
        {
            Position = ContentStartPosition,
            Size = ContentSize,
            AutoHideScrollBar = true,
        };
        scroll.ContentNode.FitWidth = true;
        scroll.ContentNode.FitContents = true;
        scroll.AttachNode(this);
        scroll.RecalculateSizes();

        Refresh();
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        scroll = null;
        renderedSender = null;
        renderedContent = null;

        base.OnFinalize(addon);
    }

    protected override unsafe void OnDraw(AtkUnitBase* addon)
    {
        var last = Plugin.MessageManager.LastMessage;

        if (!ReferenceEquals(last.Sender, renderedSender) || !ReferenceEquals(last.Message, renderedContent))
            Refresh();

        base.OnDraw(addon);
    }

    public unsafe void SetVisible(bool visible)
    {
        if (InternalAddon is null || InternalAddon->IsVisible == visible)
            return;

        InternalAddon->IsVisible = visible;
        RootNode.IsVisible = visible;
    }

    private void Refresh()
    {
        if (scroll is null)
            return;

        var last = Plugin.MessageManager.LastMessage;
        renderedSender = last.Sender;
        renderedContent = last.Message;

        var builder = new StringBuilder();
        builder.AppendLine("Sender content");
        AppendSeString(builder, last.Sender);
        builder.AppendLine();
        builder.AppendLine("Message content");
        AppendSeString(builder, last.Message);

        var list = scroll.ContentNode;
        list.Clear();

        var width = scroll.ContentNode.Width;

        foreach (var line in builder.ToString().Split('\n'))
        {
            list.AddNode(new TextNode
            {
                TextFlags = TextFlags.WordWrap | TextFlags.MultiLine,
                Size = new Vector2(width, 16.0f),
                FontSize = 11,
                LineSpacing = 14,
                String = line.TrimEnd('\r'),
            });
        }

        list.RecalculateLayout();
        scroll.RecalculateSizes();
    }

    private static void AppendSeString(StringBuilder builder, SeString? seString)
    {
        if (seString is null)
        {
            builder.AppendLine("Nothing to show");
            return;
        }

        builder.AppendLine(new ReadOnlySeString(seString.Encode()).ToMacroString());

        foreach (var payload in seString.Payloads)
            builder.AppendLine($"- {payload.Type}: {payload}");
    }
}
