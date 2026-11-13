using System.Numerics;
using Chat2Ex.Code;
using Chat2Ex.Util;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;

namespace Chat2Ex.Native;

/// <summary>
///     Borderless popup that previews the message currently being typed, including auto-translate
///     payloads and chat colours.
/// </summary>
public sealed class NativeInputPreview : NativeAddon
{
    private const float Padding = 6.0f;
    private const float MinimumWidth = 200.0f;

    private readonly Plugin Plugin;

    private MessageRenderer? renderer;
    private VerticalListNode? contentList;
    private Message? renderedMessage;
    private float renderedWidth = -1.0f;

    public NativeInputPreview(Plugin plugin) => Plugin = plugin;

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        if (WindowNode is WindowNode windowNode)
        {
            windowNode.TitleNode.IsVisible = false;
            windowNode.SubtitleNode.IsVisible = false;
            windowNode.ShowCloseButton = false;
            windowNode.ShowConfigButton = false;
            windowNode.ShowHelpButton = false;
        }

        renderer = new MessageRenderer(Plugin);

        contentList = new VerticalListNode
        {
            Position = new Vector2(Padding, Padding),
            ItemSpacing = 0.0f,
        };
        contentList.AttachNode(this);
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        renderer?.Dispose();
        renderer = null;
        contentList = null;
        renderedMessage = null;

        base.OnFinalize(addon);
    }

    public unsafe void SetVisible(bool visible)
    {
        if (InternalAddon is null || InternalAddon->IsVisible == visible)
            return;

        InternalAddon->IsVisible = visible;
        RootNode.IsVisible = visible;
    }

    public override void Dispose()
    {
        renderer?.Dispose();
        renderer = null;

        base.Dispose();
    }

    /// <summary>
    ///     Renders the preview for the given message and places the popup next to the chat window.
    /// </summary>
    public void Show(Message message, Vector2 chatPosition, Vector2 chatSize)
    {
        if (contentList is null || renderer is null)
            return;

        var width = Math.Max(MinimumWidth, chatSize.X);
        var tab = Plugin.CurrentTab;

        if (!ReferenceEquals(renderedMessage, message) || Math.Abs(renderedWidth - width) > 0.5f)
        {
            renderedMessage = message;
            renderedWidth = width;

            contentList.Clear();

            var row = renderer.Build(message, tab, width - Padding * 2.0f, (_, _, _) => { });
            contentList.AddNode(row);

            var height = row.Height + Padding * 2.0f + 6.0f;
            Size = new Vector2(width, height);
            contentList.Width = width - Padding * 2.0f;
            contentList.RecalculateLayout();
        }

        var position = Plugin.Config.PreviewPosition switch
        {
            PreviewPosition.Top => new Vector2(chatPosition.X, Math.Max(0.0f, chatPosition.Y - Size.Y)),
            PreviewPosition.Bottom => new Vector2(chatPosition.X, chatPosition.Y + chatSize.Y),
            _ => new Vector2(chatPosition.X, chatPosition.Y + chatSize.Y),
        };

        SetWindowPosition(position);
        SetVisible(true);
    }
}
