using System.Numerics;
using Chat2Ex.Code;
using Chat2Ex.Resources;
using Chat2Ex.Util;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.ContextMenu;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace Chat2Ex.Native;

/// <summary>
///     Native replacement for the ImGui DB viewer: browse the most recent stored messages with a
///     simple text filter.
/// </summary>
public sealed class NativeDbViewer : NativeAddon
{
    private const int MaxFetchedMessages = 500;
    private const int MaxRenderedMessages = 200;
    private const float SearchHeight = 28.0f;
    private const float FooterHeight = 62.0f;

    private readonly Plugin Plugin;

    private TextInputNode? searchNode;
    private CheckboxNode? currentCharacterNode;
    private TextNode? statusNode;
    private ResNode? separatorNode;
    private ScrollingNode<VerticalListNode>? scroll;
    private MessageRenderer? renderer;
    private ContextMenu? contextMenu;

    private string searchTerm = string.Empty;
    private bool onlyCurrentCharacter = true;
    private bool needsReload;

    public NativeDbViewer(Plugin plugin) => Plugin = plugin;

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        var start = ContentStartPosition;
        var size = ContentSize;

        renderer = new MessageRenderer(Plugin);
        contextMenu = new ContextMenu();

        searchNode = new TextInputNode
        {
            Position = start,
            Size = new Vector2(size.X, SearchHeight),
            MaxCharacters = 128,
            PlaceholderString = Language.DbViewer_SearcHint,
            OnInputReceived = OnSearchChanged,
            OnInputComplete = OnSearchSubmitted,
        };
        searchNode.AttachNode(this);

        currentCharacterNode = new CheckboxNode
        {
            Position = new Vector2(start.X, start.Y + SearchHeight + 4.0f),
            IsChecked = onlyCurrentCharacter,
            String = "Only current character",
        };
        currentCharacterNode.OnClick = value =>
        {
            onlyCurrentCharacter = value;
            needsReload = true;
        };
        currentCharacterNode.AttachNode(this);

        statusNode = new TextNode
        {
            Position = new Vector2(start.X, start.Y + SearchHeight + 30.0f),
            Size = new Vector2(size.X, 16.0f),
            FontSize = 12,
            LineSpacing = 15,
        };
        statusNode.AttachNode(this);

        separatorNode = new ResNode
        {
            Position = new Vector2(start.X, start.Y + SearchHeight + 48.0f),
            Size = new Vector2(size.X, 1.0f),
            NodeFlags = NodeFlags.Visible | NodeFlags.Enabled | NodeFlags.Fill,
        };
        separatorNode.AttachNode(this);

        scroll = new ScrollingNode<VerticalListNode>
        {
            Position = new Vector2(start.X, start.Y + SearchHeight + 52.0f),
            Size = new Vector2(size.X, size.Y - SearchHeight - FooterHeight),
            AutoHideScrollBar = true,
        };
        scroll.ContentNode.FitWidth = true;
        scroll.ContentNode.FitContents = true;
        scroll.AttachNode(this);
        scroll.RecalculateSizes();

        needsReload = true;
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        searchNode = null;
        currentCharacterNode = null;
        statusNode = null;
        separatorNode = null;
        scroll = null;

        renderer?.Dispose();
        renderer = null;

        contextMenu?.Dispose();
        contextMenu = null;

        base.OnFinalize(addon);
    }

    protected override unsafe void OnDraw(AtkUnitBase* addon)
    {
        if (needsReload)
        {
            needsReload = false;
            Reload();
        }

        base.OnDraw(addon);
    }

    public unsafe void SetVisible(bool visible)
    {
        if (InternalAddon is null || InternalAddon->IsVisible == visible)
            return;

        InternalAddon->IsVisible = visible;
        RootNode.IsVisible = visible;

        if (visible)
            needsReload = true;
    }

    private void OnSearchChanged(ReadOnlySeString text)
    {
        searchTerm = text.ToString();
        needsReload = true;
    }

    private void OnSearchSubmitted(ReadOnlySeString text)
    {
        searchTerm = text.ToString();
        needsReload = true;
    }

    private void Reload()
    {
        if (scroll is null || renderer is null || statusNode is null)
            return;

        var list = scroll.ContentNode;
        list.Clear();

        List<Message> messages;

        try
        {
            using var enumerator = Plugin.MessageManager.Store.GetMostRecentMessages(
                onlyCurrentCharacter ? Plugin.MessageManager.CurrentContentId : null,
                null,
                MaxFetchedMessages);

            messages = enumerator.ToList();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to load messages for the native DB viewer");
            statusNode.String = "Failed to load messages, see the log for details.";
            return;
        }

        if (searchTerm.Length > 0)
        {
            messages = messages
                .Where(message => ContainsTerm(message, searchTerm))
                .ToList();
        }

        var total = messages.Count;
        var rendered = messages.TakeLast(MaxRenderedMessages).ToList();

        statusNode.String = total > rendered.Count
            ? $"{total} matches, showing the last {rendered.Count}"
            : $"{total} matches";

        var width = scroll.ContentNode.Width;

        // Timestamps are rendered according to the currently selected tab's display setting.
        var displayTab = Plugin.CurrentTab;
        foreach (var message in rendered)
            list.AddNode(renderer.Build(message, displayTab, width, (msg, _, action) =>
            {
                if (action == LinkAction.Context)
                    ShowMenu(msg);
            }));

        list.RecalculateLayout();
        scroll.RecalculateSizes();
        scroll.ScrollToStart();
    }

    private static bool ContainsTerm(Message message, string term) =>
        ChunksContain(message.Sender, term) || ChunksContain(message.Content, term);

    private static bool ChunksContain(IEnumerable<Chunk> chunks, string term) =>
        chunks.OfType<TextChunk>().Any(chunk => chunk.Content.Contains(term, StringComparison.OrdinalIgnoreCase));

    public override void Dispose()
    {
        renderer?.Dispose();
        renderer = null;

        contextMenu?.Dispose();
        contextMenu = null;

        base.Dispose();
    }

    private void ShowMenu(Message message)
    {
        if (contextMenu is null)
            return;

        contextMenu.Clear();
        contextMenu.AddItem("Copy", () => NativeCallback.Safe(() =>
        {
            var text = string.Concat(message.Sender.Concat(message.Content).OfType<TextChunk>().Select(chunk => chunk.Content));
            Dalamud.Bindings.ImGui.ImGui.SetClipboardText(text);
        }, "copying a message"));

        contextMenu.AddItem("Delete from database", () => NativeCallback.Safe(() =>
        {
            Plugin.MessageManager.Store.DeleteMessage(message.Id);
            needsReload = true;
        }, "deleting a message"));

        // The menu is opened on the next tick and not bound to an owner addon, our window is not a
        // real game addon and binding it crashes the game.
        Plugin.Framework.RunOnTick(() =>
        {
            if (contextMenu is null)
                return;

            contextMenu.Open(false, true);
        });
    }
}
