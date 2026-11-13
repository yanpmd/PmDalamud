using System.Numerics;
using Chat2Ex.Code;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace Chat2Ex.Native;

/// <summary>
///     Small popup next to the chat window that explains the slash command currently being typed.
/// </summary>
public sealed class NativeCommandHelp : NativeAddon
{
    private const float PopupWidth = 350.0f;
    private const float Padding = 10.0f;

    private readonly Plugin Plugin;

    private TextNode? textNode;
    private ResNode? background;

    public NativeCommandHelp(Plugin plugin) => Plugin = plugin;

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        // Hide the window chrome since this is a borderless tooltip-like popup.
        if (WindowNode is WindowNode windowNode)
        {
            windowNode.TitleNode.IsVisible = false;
            windowNode.SubtitleNode.IsVisible = false;
            windowNode.ShowCloseButton = false;
            windowNode.ShowConfigButton = false;
            windowNode.ShowHelpButton = false;
        }

        background = new ResNode
        {
            Position = new Vector2(6.0f, 6.0f),
            Size = new Vector2(PopupWidth - 12.0f, 20.0f),
            NodeFlags = NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.Fill,
        };
        background.AttachNode(this);

        textNode = new TextNode
        {
            Position = new Vector2(12.0f, 10.0f),
            TextFlags = TextFlags.WordWrap | TextFlags.MultiLine,
            FontSize = 12,
            LineSpacing = 15,
            AlignmentType = AlignmentType.TopLeft,
        };
        textNode.AttachNode(this);
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        textNode = null;
        background = null;

        base.OnFinalize(addon);
    }

    public unsafe void SetVisible(bool visible)
    {
        if (InternalAddon is null || InternalAddon->IsVisible == visible)
            return;

        InternalAddon->IsVisible = visible;
        RootNode.IsVisible = visible;
    }

    /// <summary>
    ///     Shows the given description next to the chat window.
    /// </summary>
    public void Show(ReadOnlySeString description, Vector2 chatPosition, Vector2 chatSize)
    {
        if (textNode is null || background is null)
            return;

        textNode.String = description;

        var textWidth = PopupWidth - Padding * 2.0f;
        var drawWidth = textNode.GetTextDrawSize(false).X;
        var lines = Math.Max(1, (int)MathF.Ceiling(drawWidth / Math.Max(1.0f, textWidth)));
        var textHeight = lines * 16.0f;

        var height = textHeight + Padding * 2.0f + 8.0f;
        Size = new Vector2(PopupWidth, height);

        textNode.Size = new Vector2(textWidth, textHeight);
        background.Size = new Vector2(PopupWidth - 12.0f, height - 14.0f);

        var x = Plugin.Config.CommandHelpSide switch
        {
            CommandHelpSide.Right => chatPosition.X + chatSize.X,
            CommandHelpSide.Left => chatPosition.X - PopupWidth,
            _ => chatPosition.X,
        };

        SetWindowPosition(new Vector2(x, Math.Max(0.0f, chatPosition.Y)));
        SetVisible(true);
    }
}
