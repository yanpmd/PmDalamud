using System.Numerics;
using System.Text;
using Chat2Ex.Code;
using Chat2Ex.GameFunctions.Types;
using Chat2Ex.Resources;
using Chat2Ex.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.ContextMenu;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace Chat2Ex.Native;

/// <summary>
///     Native (AtkUnitBase) chat window, replacing the ImGui chat log.
/// </summary>
/// <remarks>
///     A window with <see cref="TabIndex" /> -1 is the main chat log (tab bar, all tabs).
///     Any other index is a popout window pinned to a single tab.
/// </remarks>
public sealed class NativeChatWindow : NativeAddon, IChatWindow
{
    private const float TabBarHeight = 24.0f;
    private uint ChatFontSize => (uint)Math.Clamp(Plugin.Config.ChatFontSize, 10, 24);

    private float InputHeight => ChatFontSize + 16.0f;

    // A small ">" marker on the left of the input row; the channel name sits above it in a
    // fixed-size label with a slightly smaller font.
    private const float ChannelMarkerWidth = 22.0f;
    private const float ChannelNameWidth = 260.0f;
    private const float ChannelNameHeight = 18.0f;
    private uint ChannelNameFontSize => (uint) Math.Clamp(ChatFontSize - 2, 9, 22);

    private const float TabMenuButtonWidth = 26.0f;
    private const float DragStripHeight = 16.0f;

    // The handle in the top right corner that resizes the window.
    private const float ResizeHandleSize = 16.0f;
    private const float MinWindowWidth = 320.0f;
    private const float MinWindowHeight = 160.0f;
    private const float MaxWindowWidth = 1920.0f;
    private const float MaxWindowHeight = 1600.0f;

    private readonly Plugin Plugin;

    /// <summary>
    ///     The native context menu is shared by every chat window and outlives them. The game keeps
    ///     a pointer to the event interface of an open menu, releasing the menu together with the
    ///     window that opened it crashes the game as soon as an entry is clicked.
    /// </summary>
    private ContextMenu Menu => Plugin.ChatUi.ContextMenu;

    private TabBarNode? tabBar;
    private ScrollingNode<VerticalListNode>? messageScroll;
    private TextInputNode? inputNode;
    private TextNode? channelNode;
    private TextNode? channelMarker;
    private CollisionNode? channelMarkerHitbox;
    private TextButtonNode? tabMenuButton;
    private WindowBackgroundTextureNode? backgroundTexture;
    private CollisionNode? resizeHandle;
    private TextNode? resizeHandleLabel;
    private CollisionNode? emptyHeader;
    private MessageRenderer? renderer;
    private NativeTextPrompt? renamePrompt;

    // Resize state, the window is resized while the handle is dragged.
    private bool isResizing;
    private Vector2 resizeStartMouse;
    private Vector2 resizeStartSize;
    private Vector2 resizeStartPos;

    // Drag state, the window is moved while the invisible header strip is dragged.
    private bool isDragging;
    private Vector2 dragStartMouse;
    private Vector2 dragStartPos;

    // Change tracking so we only rebuild when needed.
    private int renderedTabIndex = -1;
    private int renderedTabCount = -1;
    private string renderedTabListSignature = string.Empty;
    private string renderedTabName = string.Empty;
    private int renderedMessageCount = -1;
    private Guid renderedFirstMessageId = Guid.Empty;
    private Guid renderedLastMessageId = Guid.Empty;
    private uint renderedFontSize;
    private bool renderedLockPosition;
    private string renderedInput = string.Empty;
    private float inputStartX;
    private float inputTotalWidth;
    private float messageTop;
    private float contentBottom;

    // Messages are added to the list over multiple frames so large histories do not stall the game.
    private readonly List<Message> pendingMessages = [];
    private int pendingMessageIndex;
    private Tab? pendingTab;
    private string renderedChannel = string.Empty;
    private float renderedAlpha = -1.0f;

    public NativeChatWindow(Plugin plugin, int tabIndex = -1)
    {
        Plugin = plugin;
        TabIndex = tabIndex;
    }

    /// <summary>
    ///     Index into <see cref="Configuration.Tabs" />, or -1 for the main window.
    /// </summary>
    public int TabIndex { get; }

    /// <summary>
    ///     Timestamp (Environment.TickCount64) of the last visible activity, used for "hide when inactive".
    /// </summary>
    public long LastActivityTime { get; private set; } = Environment.TickCount64;

    /// <summary>
    ///     Marks the window as active, so that it does not get hidden by the inactivity timeout.
    /// </summary>
    public void MarkActivity() => LastActivityTime = Environment.TickCount64;

    public Vector2 LastWindowPos { get; set; }
    public Vector2 LastWindowSize { get; set; }
    public HideState CurrentHideState { get; set; }

    private bool IsMainWindow => TabIndex < 0;

    private int MaxRenderedMessages => Math.Clamp(Plugin.Config.MaxLinesToRender, 10, 150);

    private Tab TargetTab => TabIndex >= 0 && TabIndex < Plugin.Config.Tabs.Count
        ? Plugin.Config.Tabs[TabIndex]
        : Plugin.CurrentTab;

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        renderer = new MessageRenderer(Plugin);
        renderer.RebuildRequested += InvalidateRenderedMessages;

        // Closing the window is handled by the chat log, letting the game close it would run the
        // native close callback on a manually built addon and crash the game.
        RespectCloseAll = false;

        // The addon is recreated on every open, a drag or resize state left over from the previous
        // instance would make the window jump on the first mouse move.
        isDragging = false;
        isResizing = false;

        if (WindowNode is WindowNode windowNode)
        {
            backgroundTexture = ApplyChrome(windowNode);

            // The game drags a window by its own header collision node. Dragging an addon without
            // an ULD crashes the game, so the drag is implemented by hand further down.
            windowNode.HeaderCollisionNode.AddEvent(AtkEventType.MouseDown, OnHeaderMouseDown);
            windowNode.HeaderCollisionNode.AddEvent(AtkEventType.MouseMove, OnHeaderMouseMove);
            windowNode.HeaderCollisionNode.AddEvent(AtkEventType.MouseUp, OnHeaderMouseUp);
        }

        // Without a header collision node the game does not try to drag the window, which would
        // crash for an addon that has no ULD.
        emptyHeader = new CollisionNode
        {
            Size = Vector2.Zero,
            IsVisible = false,
        };
        emptyHeader.AttachNode(this);

        if (InternalAddon is not null)
            InternalAddon->WindowHeaderCollisionNode = emptyHeader.Node;

        // A small handle in the top right corner that resizes the window.
        resizeHandleLabel = new TextNode
        {
            Size = new Vector2(ResizeHandleSize, ResizeHandleSize),
            FontSize = 9,
            LineSpacing = 11,
            AlignmentType = AlignmentType.Center,
            TextColor = new Vector4(0.65f, 0.65f, 0.65f, 0.8f),
            String = "///",
        };
        resizeHandleLabel.AttachNode(this);

        resizeHandle = new CollisionNode
        {
            Size = new Vector2(ResizeHandleSize, ResizeHandleSize),
            ShowClickableCursor = true,
        };
        resizeHandle.AddEvent(AtkEventType.MouseDown, OnResizeMouseDown);
        resizeHandle.AddEvent(AtkEventType.MouseMove, OnResizeMouseMove);
        resizeHandle.AddEvent(AtkEventType.MouseUp, OnResizeMouseUp);
        resizeHandle.AttachNode(this);

        if (IsMainWindow)
        {
            tabBar = new TabBarNode();
            tabBar.AttachNode(this);

            tabMenuButton = new TextButtonNode
            {
                Size = new Vector2(TabMenuButtonWidth, TabBarHeight),
                String = "...",
                OnClick = ShowTabMenu,
            };
            tabMenuButton.AttachNode(this);
        }

        messageScroll = new ScrollingNode<VerticalListNode>
        {
            AutoHideScrollBar = true,
        };
        messageScroll.ContentNode.FitWidth = true;
        messageScroll.ContentNode.FitContents = true;
        messageScroll.ContentNode.ItemSpacing = 2.0f;
        messageScroll.AttachNode(this);

        // The message area keeps the resize going when the pointer leaves the handle.
        messageScroll.ScrollingCollisionNode.AddEvent(AtkEventType.MouseMove, OnResizeMouseMove);
        messageScroll.ScrollingCollisionNode.AddEvent(AtkEventType.MouseUp, OnResizeMouseUp);

        // Fixed-size channel name above the input row, with a slightly smaller font.
        channelNode = new TextNode
        {
            Size = new Vector2(ChannelNameWidth, ChannelNameHeight),
            FontSize = ChannelNameFontSize,
            LineSpacing = ChannelNameFontSize + 3,
            AlignmentType = AlignmentType.TopLeft,
            TextColor = new Vector4(0.68f, 0.68f, 0.68f, 1.0f),
            TextOutlineColor = new Vector4(0.0f, 0.0f, 0.0f, 0.85f),
        };
        channelNode.AttachNode(this);

        // A small ">" marker with an invisible hitbox instead of a full button.
        channelMarker = new TextNode
        {
            Size = new Vector2(ChannelMarkerWidth, InputHeight),
            FontSize = ChatFontSize,
            LineSpacing = ChatFontSize + 3,
            AlignmentType = AlignmentType.Center,
            TextColor = new Vector4(0.85f, 0.85f, 0.85f, 1.0f),
            TextOutlineColor = new Vector4(0.0f, 0.0f, 0.0f, 0.85f),
            String = ">",
        };
        channelMarker.AttachNode(this);

        channelMarkerHitbox = new CollisionNode
        {
            Size = new Vector2(ChannelMarkerWidth, InputHeight),
            ShowClickableCursor = true,
        };
        channelMarkerHitbox.AddEvent(AtkEventType.MouseClick, ShowChannelMenu);
        channelMarkerHitbox.AttachNode(this);

        inputNode = new TextInputNode
        {
            Size = new Vector2(inputTotalWidth - ChannelMarkerWidth, InputHeight),
            MaxCharacters = 500,
            EnableCompletion = true,
            OnInputComplete = OnInputComplete,
            OnInputReceived = OnInputReceived,
            OnFocused = OnInputFocused,
            OnFocusLost = OnInputFocusLost,
        };
        inputNode.AttachNode(this);

        // The input component keeps its own text nodes, so the font size has to be applied to them.
        inputNode.CurrentTextNode.FontSize = ChatFontSize;
        inputNode.CurrentTextNode.LineSpacing = ChatFontSize + 3;
        inputNode.PlaceholderTextNode.FontSize = ChatFontSize;
        inputNode.PlaceholderTextNode.LineSpacing = ChatFontSize + 3;

        ApplyLayout();

        if (IsMainWindow)
            RebuildTabs();

        SyncChannelLabel();
    }

    /// <summary>
    ///     Positions the window content for the current window size. Called again whenever the window
    ///     is resized.
    /// </summary>
    private void ApplyLayout()
    {
        if (messageScroll is null)
            return;

        var contentStart = ContentStartPosition;
        var contentSize = ContentSize;

        // The chat is borderless: hide all window chrome and reclaim the (now invisible) title bar
        // so the content starts right at the top edge of the addon.
        var headerHeight = 0.0f;

        if (WindowNode is WindowNode windowNode)
        {
            headerHeight = windowNode.HeaderContainerNode.Height;

            if (headerHeight <= 0.0f)
                headerHeight = 24.0f;
        }

        // Keep a thin invisible strip at the top so the window can still be dragged by its header.
        var top = Math.Max(2.0f, contentStart.Y - headerHeight + DragStripHeight);
        contentStart = new Vector2(contentStart.X, top);
        contentSize = new Vector2(contentSize.X, contentSize.Y + (ContentStartPosition.Y - top));

        if (tabBar is not null)
        {
            tabBar.Position = contentStart;
            tabBar.Size = new Vector2(contentSize.X - TabMenuButtonWidth, TabBarHeight);
        }

        if (tabMenuButton is not null)
        {
            tabMenuButton.Position = new Vector2(contentStart.X + contentSize.X - TabMenuButtonWidth, contentStart.Y);
            tabMenuButton.Size = new Vector2(TabMenuButtonWidth, TabBarHeight);
        }

        var scrollTop = contentStart.Y + (IsMainWindow ? TabBarHeight + 4.0f : 0.0f);

        messageScroll.Position = new Vector2(contentStart.X, scrollTop);

        inputStartX = contentStart.X;
        inputTotalWidth = contentSize.X;
        messageTop = scrollTop;
        contentBottom = contentStart.Y + contentSize.Y;

        // The resize handle sits in the top right corner of the message area.
        var handlePosition = new Vector2(contentStart.X + contentSize.X - ResizeHandleSize, scrollTop + 2.0f);

        if (resizeHandle is not null)
        {
            resizeHandle.Position = handlePosition;
            resizeHandle.Size = new Vector2(ResizeHandleSize, ResizeHandleSize);
        }

        if (resizeHandleLabel is not null)
        {
            resizeHandleLabel.Position = handlePosition;
            resizeHandleLabel.Size = new Vector2(ResizeHandleSize, ResizeHandleSize);
        }

        // The font size is only a field of a node, the glyph cache has to be rebuilt, or the
        // text is drawn with the glyphs of the previous size.
        if (channelNode is not null)
            MessageRenderer.ApplyFontSize(channelNode);

        if (channelMarker is not null)
            MessageRenderer.ApplyFontSize(channelMarker);

        if (resizeHandleLabel is not null)
            MessageRenderer.ApplyFontSize(resizeHandleLabel);

        ApplyInputLayout();
    }

    /// <summary>
    ///     Starts the resize when the handle is pressed.
    /// </summary>
    private unsafe void OnResizeMouseDown(AtkEventListener* listener, AtkEventType type, int param, AtkEvent* atkEvent, AtkEventData* data)
    {
        if (data is null || !data->IsLeftClick)
            return;

        isResizing = true;
        resizeStartMouse = data->MousePosition;
        resizeStartSize = Size;
        resizeStartPos = LastWindowPos;
    }

    /// <summary>
    ///     Resizes the window while the handle is dragged.
    /// </summary>
    private unsafe void OnResizeMouseMove(AtkEventListener* listener, AtkEventType type, int param, AtkEvent* atkEvent, AtkEventData* data)
    {
        if (!isResizing || data is null)
            return;

        ApplyResize(data->MousePosition);
    }

    /// <summary>
    ///     Ends the resize and stores the new size.
    /// </summary>
    private unsafe void OnResizeMouseUp(AtkEventListener* listener, AtkEventType type, int param, AtkEvent* atkEvent, AtkEventData* data)
    {
        if (!isResizing)
            return;

        isResizing = false;

        if (!IsMainWindow)
            return;

        Plugin.Config.ChatWindowSize = Size;
        Plugin.SaveConfig();
    }

    /// <summary>
    ///     Applies the new window size. The bottom right corner stays in place, the window grows to
    ///     the left and upwards.
    /// </summary>
    private void ApplyResize(Vector2 mousePosition)
    {
        var delta = mousePosition - resizeStartMouse;

        var width = Math.Clamp(resizeStartSize.X - delta.X, MinWindowWidth, MaxWindowWidth);
        var height = Math.Clamp(resizeStartSize.Y - delta.Y, MinWindowHeight, MaxWindowHeight);

        if (Math.Abs(width - Size.X) < 0.5f && Math.Abs(height - Size.Y) < 0.5f)
            return;

        Size = new Vector2(width, height);

        var position = new Vector2(
            resizeStartPos.X + (resizeStartSize.X - width),
            resizeStartPos.Y + (resizeStartSize.Y - height));

        SetWindowPosition(position);

        ApplyLayout();

        // The message rows are laid out for the current width, so they have to be rebuilt.
        InvalidateRenderedMessages();
    }

    /// <summary>
    ///     Removes the window frame, title text and all header buttons, but keeps the frosted glass
    ///     background texture so the chat stays readable over the world. The invisible header strip is
    ///     kept so the window can still be dragged around like any other game window.
    /// </summary>
    private WindowBackgroundTextureNode? ApplyChrome(WindowNode node)
    {
        node.ShowCloseButton = false;
        node.ShowConfigButton = false;
        node.ShowHelpButton = false;

        node.TitleNode.IsVisible = false;
        node.SubtitleNode.IsVisible = false;

        node.BackgroundImageNode.IsVisible = false;
        node.BorderTextureNode.IsVisible = false;
        node.DividingLineNode.IsVisible = false;

        node.BackgroundTextureNode.IsVisible = true;

        return node.BackgroundTextureNode;
    }

    /// <summary>
    ///     Starts the drag of the window when the header of the window is pressed.
    /// </summary>
    private unsafe void OnHeaderMouseDown(AtkEventListener* listener, AtkEventType type, int param, AtkEvent* atkEvent, AtkEventData* data)
    {
        if (data is null || !data->IsLeftClick)
            return;

        // The lock setting disables the drag, the window then stays where it is.
        if (Plugin.Config.LockWindowPosition)
            return;

        isDragging = true;
        dragStartMouse = data->MousePosition;
        dragStartPos = LastWindowPos;
    }

    /// <summary>
    ///     Moves the window while the header is held.
    /// </summary>
    private unsafe void OnHeaderMouseMove(AtkEventListener* listener, AtkEventType type, int param, AtkEvent* atkEvent, AtkEventData* data)
    {
        if (!isDragging || data is null)
            return;

        var delta = data->MousePosition - dragStartMouse;

        // The mouse position is in screen space, so a jump means the event came from a different
        // node and cannot be used to move the window.
        if (Math.Abs(delta.X) > 500.0f || Math.Abs(delta.Y) > 500.0f)
            return;

        SetWindowPosition(dragStartPos + delta);
    }

    /// <summary>
    ///     Ends the drag of the window.
    /// </summary>
    private unsafe void OnHeaderMouseUp(AtkEventListener* listener, AtkEventType type, int param, AtkEvent* atkEvent, AtkEventData* data)
    {
        isDragging = false;
    }

    /// <summary>
    ///     Picks up the "lock the window position" setting without recreating the window.
    /// </summary>
    private void SyncLockPosition()
    {
        var locked = Plugin.Config.LockWindowPosition;

        if (locked == renderedLockPosition)
            return;

        renderedLockPosition = locked;

        // A locked window cannot be dragged, the handle stays usable for resizing.
        if (locked)
            isDragging = false;
    }

    /// <summary>
    ///     Picks up font size changes made in the settings without recreating the window.
    /// </summary>
    private void SyncFontSize()
    {
        var fontSize = ChatFontSize;
        var changed = false;

        if (inputNode is not null && inputNode.CurrentTextNode.FontSize != fontSize)
        {
            inputNode.CurrentTextNode.FontSize = fontSize;
            inputNode.CurrentTextNode.LineSpacing = fontSize + 3;
            inputNode.PlaceholderTextNode.FontSize = fontSize;
            inputNode.PlaceholderTextNode.LineSpacing = fontSize + 3;
            changed = true;
        }

        var nameFontSize = ChannelNameFontSize;

        if (channelNode is not null && channelNode.FontSize != nameFontSize)
        {
            channelNode.FontSize = nameFontSize;
            channelNode.LineSpacing = nameFontSize + 3;
            changed = true;
        }

        if (channelMarker is not null && channelMarker.FontSize != fontSize)
        {
            channelMarker.FontSize = fontSize;
            channelMarker.LineSpacing = fontSize + 3;
            changed = true;
        }

        if (changed)
        {
            // The font size is only a field of a node, the glyph cache has to be rebuilt, or the
            // text is drawn with the glyphs of the previous size.
            if (inputNode is not null)
            {
                MessageRenderer.ApplyFontSize(inputNode.CurrentTextNode);
                MessageRenderer.ApplyFontSize(inputNode.PlaceholderTextNode);
            }

            if (channelNode is not null)
                MessageRenderer.ApplyFontSize(channelNode);

            if (channelMarker is not null)
                MessageRenderer.ApplyFontSize(channelMarker);

            if (resizeHandleLabel is not null)
                MessageRenderer.ApplyFontSize(resizeHandleLabel);

            ApplyInputLayout();

            // Re-truncate the fixed-size channel name for the new font metrics.
            renderedChannel = string.Empty;
            SyncChannelLabel();
        }
    }

    /// <summary>
    ///     Fixed layout: channel name above, a small ">" marker and the aligned input box below.
    /// </summary>
    private void ApplyInputLayout()
    {
        if (channelNode is null || channelMarker is null || channelMarkerHitbox is null || inputNode is null)
            return;

        var inputY = contentBottom - InputHeight;
        var markerWidth = ChannelMarkerWidth;

        channelNode.Position = new Vector2(inputStartX, inputY - ChannelNameHeight - 2.0f);
        channelNode.Size = new Vector2(Math.Min(inputTotalWidth, ChannelNameWidth), ChannelNameHeight);

        // All three nodes share exactly the same Y and height so they line up visually.
        channelMarker.Position = new Vector2(inputStartX, inputY);
        channelMarker.Size = new Vector2(markerWidth, InputHeight);

        channelMarkerHitbox.Position = new Vector2(inputStartX, inputY);
        channelMarkerHitbox.Size = new Vector2(markerWidth, InputHeight);

        inputNode.Position = new Vector2(inputStartX + markerWidth, inputY);
        inputNode.Size = new Vector2(Math.Max(50.0f, inputTotalWidth - markerWidth), InputHeight);

        if (messageScroll is not null)
        {
            var scrollHeight = Math.Max(40.0f, inputY - ChannelNameHeight - 6.0f - messageTop);
            messageScroll.Size = new Vector2(inputTotalWidth, scrollHeight);
            messageScroll.RecalculateSizes();
        }
    }

    /// <summary>
    ///     Applies the configured opacity to the glass background panel.
    /// </summary>
    private void ApplyBackgroundAlpha()
    {
        if (backgroundTexture is null)
            return;

        backgroundTexture.Alpha = Math.Clamp(Plugin.Config.ChatBackgroundOpacity / 100.0f, 0.0f, 1.0f);
    }

    /// <summary>
    ///     Forces a full message rebuild on the next frame, e.g. after the channel filters changed.
    /// </summary>
    public void InvalidateRenderedMessages()
    {
        renderedTabIndex = -1;
        renderedTabName = string.Empty;
        renderedMessageCount = -1;
        renderedFirstMessageId = Guid.Empty;
        renderedLastMessageId = Guid.Empty;
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        Plugin.Log.Info($"[Chat2Ex] Finalizing window addon {(addon is null ? 0u : addon->Id)}");

        // The native node tree is destroyed with the addon, so drop all node references.
        tabBar = null;
        messageScroll = null;
        inputNode = null;
        channelNode = null;
        channelMarker = null;
        channelMarkerHitbox = null;
        tabMenuButton = null;
        renamePrompt = null;
        backgroundTexture = null;
        resizeHandle = null;
        resizeHandleLabel = null;
        emptyHeader = null;

        renderer?.Dispose();
        renderer = null;

        // Only close the menu, its event interface has to stay alive for the game, see ContextMenu.
        Plugin.ChatUi.ContextMenu.Close();

        base.OnFinalize(addon);
    }

    /// <summary>
    ///     Shows or hides the window without closing the addon (KTK closes and destroys the tree on Close).
    /// </summary>
    public unsafe void SetVisible(bool visible)
    {
        if (InternalAddon is null)
            return;

        if (InternalAddon->IsVisible == visible)
            return;

        InternalAddon->IsVisible = visible;
        RootNode.IsVisible = visible;

        if (visible)
            LastActivityTime = Environment.TickCount64;
        else
            renderer?.CloseTooltip();
    }

    /// <summary>
    ///     Applies a pending tab switch requested by keybinds, the web interface or commands.
    /// </summary>
    public void SyncPendingTab()
    {
        if (!IsMainWindow)
            return;

        var wanted = Plugin.WantedTab;
        Plugin.WantedTab = null;

        if (wanted is not { } index)
            return;

        if (index < 0 || index >= Plugin.Config.Tabs.Count || index == Plugin.LastTab)
            return;

        SwitchTab(index);
    }

    /// <summary>
    ///     Rebuilds the rendered messages if the tab or its message list changed.
    /// </summary>
    /// <returns>Whether the visible content changed this frame.</returns>
    public bool UpdateContent()
    {
        if (messageScroll is null)
            return false;

        ProcessPendingMessages();
        renderer?.Tick();

        ApplyAlpha();
        ApplyBackgroundAlpha();
        SyncFontSize();
        SyncChannelLabel();
        SyncLockPosition();
        UpdateWindowBounds();

        // Keep the configured size up to date while the window is being resized.
        if (isResizing && IsMainWindow)
            Plugin.Config.ChatWindowSize = Size;

        if (IsMainWindow)
            SyncTabBar();

        // Rows are laid out with the current font metrics, so a size change requires a rebuild.
        if (renderedFontSize != ChatFontSize)
        {
            renderedFontSize = ChatFontSize;
            InvalidateRenderedMessages();
        }

        var tab = TargetTab;
        var tabIndex = IsMainWindow ? Plugin.LastTab : TabIndex;
        var tabName = tab.Name;

        int messageCount;
        var firstMessageId = Guid.Empty;
        var lastMessageId = Guid.Empty;
        using (var messages = tab.Messages.GetReadOnly())
        {
            messageCount = messages.Count;
            if (messageCount > 0)
            {
                firstMessageId = messages[0].Id;
                lastMessageId = messages[messageCount - 1].Id;
            }
        }

        if (tabIndex == renderedTabIndex
            && tabName == renderedTabName
            && messageCount == renderedMessageCount
            && firstMessageId == renderedFirstMessageId
            && lastMessageId == renderedLastMessageId)
            return false;

        var tabChanged = tabIndex != renderedTabIndex || tabName != renderedTabName;
        var previousCount = renderedMessageCount;

        renderedTabIndex = tabIndex;
        renderedTabName = tabName;
        renderedMessageCount = messageCount;
        renderedFirstMessageId = firstMessageId;
        renderedLastMessageId = lastMessageId;

        // Common case: only new messages arrived and the list is not at its cap yet, so existing
        // rows can be kept. Rebuilding everything would destroy and recreate hundreds of nodes.
        if (!tabChanged
            && previousCount >= 0
            && messageCount > previousCount
            && messageCount <= MaxRenderedMessages
            && pendingTab is null)
        {
            AppendMessages(tab, previousCount, messageCount);
        }
        else
        {
            RebuildMessages(tab);
        }

        LastActivityTime = Environment.TickCount64;

        return true;
    }

    /// <summary>
    ///     Appends newly arrived messages without touching the already rendered rows.
    /// </summary>
    private void AppendMessages(Tab tab, int fromIndex, int toIndex)
    {
        if (messageScroll is null || renderer is null)
            return;

        var list = messageScroll.ContentNode;
        var width = Math.Max(80.0f, list.Width - 12.0f); // keep clear of the scroll bar

        var batch = new List<KamiToolKit.BaseTypes.NodeBase>();

        using (var messages = tab.Messages.GetReadOnly())
        {
            for (var i = Math.Max(0, fromIndex); i < toIndex && i < messages.Count; i++)
                batch.Add(renderer.Build(messages[i], tab, width, HandleLinkAction));
        }

        if (batch.Count == 0)
            return;

        list.AddNode(batch);
        list.RecalculateLayout();
        messageScroll.RecalculateSizes();
        messageScroll.ScrollToEnd();
    }

    /// <summary>
    ///     Focuses the input box when the chat was activated via keybind.
    /// </summary>
    public void SyncInputFocus()
    {
        if (inputNode is null)
            return;

        var state = Plugin.ChatLog;
        state.FrameTime = Environment.TickCount64;

        // The game writes chat links ("<item>", "<flag>", typed characters from chat activation)
        // into ChatState.ChatInput from its ChatLogRefresh hook. Mirror that value into the native
        // text input, otherwise linked items / map flags never appear in the input box.
        if (IsMainWindow && state.ChatInput != renderedInput)
        {
            renderedInput = state.ChatInput;
            inputNode.String = state.ChatInput;
        }

        if (IsMainWindow && state.Activate && !inputNode.IsFocused)
        {
            inputNode.SetFocus();
            state.Activate = false;
        }

        state.InputFocused = inputNode.IsFocused;

        if (state.InputFocused)
            LastActivityTime = Environment.TickCount64;
    }

    private unsafe void UpdateWindowBounds()
    {
        if (InternalAddon is null)
            return;

        LastWindowPos = new Vector2(InternalAddon->X, InternalAddon->Y);
        LastWindowSize = Size;
    }

    private void ApplyAlpha()
    {
        var alphaConfig = TargetTab.IndependentOpacity ? TargetTab.Opacity : Plugin.Config.WindowAlpha;
        var alpha = Math.Clamp(alphaConfig / 100.0f, 0.0f, 1.0f);
        if (Math.Abs(alpha - renderedAlpha) < 0.001f)
            return;

        renderedAlpha = alpha;
        RootNode.Alpha = alpha;
    }

    private void SyncChannelLabel()
    {
        if (channelNode is null)
            return;

        var used = TargetTab.CurrentChannel;
        var channel = used.UseTempChannel ? used.TempChannel : used.Channel;
        var label = ChannelLabel(used, channel);

        if (label == renderedChannel)
            return;

        // The label has a fixed width, so just keep the text inside it.
        var nameWidth = Math.Min(inputTotalWidth, ChannelNameWidth);
        var maxChars = Math.Max(8, (int) (nameWidth / (ChannelNameFontSize * 1.05f)));
        if (label.Length > maxChars)
            label = label[..maxChars] + "…";

        renderedChannel = label;
        channelNode.String = label;

        // Keep the web interface's "current channel" in sync as well.
        if (IsMainWindow)
            Plugin.ChatLog.UpdateChannelDisplay();
    }

    /// <summary>
    ///     Uses the game's own channel label when available (it contains custom linkshell names and
    ///     the tell target), otherwise falls back to the localized channel name.
    /// </summary>
    private string ChannelLabel(UsedChannel used, InputChannel channel)
    {
        if (used.Name is { Count: > 0 })
        {
            var text = new StringBuilder();

            foreach (var chunk in used.Name)
                if (chunk is TextChunk part)
                    text.Append(part.Content);

            var cleaned = CleanChannelName(text.ToString());

            if (!string.IsNullOrWhiteSpace(cleaned))
                return cleaned;
        }

        return ChannelDisplayName(channel);
    }

    /// <summary>
    ///     Localized name for the channel, using the real linkshell name when one exists.
    /// </summary>
    private string ChannelDisplayName(InputChannel channel)
    {
        if (!Plugin.ClientState.IsLoggedIn)
            return channel.ToChatType().Name();

        try
        {
            if (channel.IsLinkshell())
            {
                var name = Chat2Ex.GameFunctions.Chat.GetLinkshellName(channel.LinkshellIndex());

                if (!string.IsNullOrWhiteSpace(name))
                    return CleanChannelName(name);
            }
            else if (channel.IsCrossLinkshell())
            {
                var name = Chat2Ex.GameFunctions.Chat.GetCrossLinkshellName(channel.LinkshellIndex());

                if (!string.IsNullOrWhiteSpace(name))
                    return CleanChannelName(name);
            }
        }
        catch (Exception)
        {
            // fall through to the generic name
        }

        return channel.ToChatType().Name();
    }

    /// <summary>
    ///     Removes game-only glyph placeholders and control characters from a channel label.
    /// </summary>
    private static string CleanChannelName(string name)
    {
        var cleaned = new StringBuilder(name.Length);

        foreach (var ch in name)
        {
            if (ch is >= '\ue000' and <= '\uf8ff' || char.IsControl(ch) || ch is '\u200b' or '\u00ad')
                continue;

            cleaned.Append(ch);
        }

        return cleaned.ToString().Trim();
    }

    /// <summary>
    ///     Opens this window's native context menu.
    /// </summary>
    /// <remarks>
    ///     The menu is opened on the next tick: opening an addon while the game is still processing
    ///     its input events makes the game close the menu again in the same frame. The menu is also
    ///     not bound to an owner addon, our window is not a real game addon and binding it crashes
    ///     the game.
    /// </remarks>
    private unsafe void OpenContextMenu()
    {
        Plugin.Log.Info($"[Chat2Ex] Opening context menu, addon id: {(InternalAddon is null ? 0u : InternalAddon->Id)}");

        Plugin.Framework.RunOnTick(() => NativeCallback.Safe(() =>
        {
            Menu.Open(false, true);
        }, "opening the context menu"));
    }

    /// <summary>
    ///     Opens the native channel picker when the input prefix is clicked.
    /// </summary>
    private void ShowChannelMenu()
    {
        NativeCallback.Safe(ShowChannelMenuInternal, "opening the channel menu");
    }

    private void ShowChannelMenuInternal()
    {
        Menu.Clear();

        var used = TargetTab.CurrentChannel;
        var active = used.UseTempChannel ? used.TempChannel : used.Channel;

        void AddChannel(Action<ReadOnlySeString, Action> add, InputChannel channel)
        {
            if (!Chat2Ex.GameFunctions.Chat.IsChannelOrExistingLinkshell(channel))
                return;

            var name = ChannelDisplayName(channel);
            add(channel == active ? $"> {name}" : name, () => NativeCallback.Safe(() => SwitchInputChannel(channel), "switching the input channel"));
        }

        foreach (var channel in new[]
                 {
                     InputChannel.Say, InputChannel.Yell, InputChannel.Shout,
                     InputChannel.Party, InputChannel.Alliance,
                     InputChannel.FreeCompany, InputChannel.PvpTeam,
                     InputChannel.NoviceNetwork, InputChannel.Tell,
                 })
        {
            AddChannel(Menu.AddItem, channel);
        }

        var linkshells = new ContextMenuSubItem { Name = "通讯贝 / Linkshells", OnClick = () => { } };
        foreach (var channel in Enum.GetValues<InputChannel>())
            if (channel is >= InputChannel.Linkshell1 and <= InputChannel.Linkshell8)
                AddChannel(linkshells.AddItem, channel);

        if (linkshells.SubItems.Count > 0)
            Menu.AddItem(linkshells);

        var crossLinkshells = new ContextMenuSubItem { Name = "跨服通讯贝 / Cross-world LS", OnClick = () => { } };
        foreach (var channel in Enum.GetValues<InputChannel>())
            if (channel is >= InputChannel.CrossLinkshell1 and <= InputChannel.CrossLinkshell8)
                AddChannel(crossLinkshells.AddItem, channel);

        if (crossLinkshells.SubItems.Count > 0)
            Menu.AddItem(crossLinkshells);

        if (Plugin.ExtraChat.ChannelNames.Count == 0)
        {
            OpenContextMenu();
            return;
        }

        var extraChat = new ContextMenuSubItem { Name = "ExtraChat 频道", OnClick = () => { } };
        foreach (var channel in Enum.GetValues<InputChannel>())
            if (channel is >= InputChannel.ExtraChatLinkshell1 and <= InputChannel.ExtraChatLinkshell8)
                AddChannel(extraChat.AddItem, channel);

        if (extraChat.SubItems.Count > 0)
            Menu.AddItem(extraChat);

        OpenContextMenu();
    }

    /// <summary>
    ///     Switches the active input channel, clearing any temporary channel from a command.
    /// </summary>
    private void SwitchInputChannel(InputChannel channel)
    {
        if (!Chat2Ex.GameFunctions.Chat.IsChannelOrExistingLinkshell(channel))
            return;

        var used = TargetTab.CurrentChannel;
        used.ResetTempChannel();
        used.SetChannel(channel);

        if (channel == InputChannel.Tell)
            used.TellTarget = null;

        Plugin.Functions.Chat.SetChannelWithExtraChat(channel);

        renderedChannel = string.Empty;
        SyncChannelLabel();
    }

    private void SyncTabBar()
    {
        if (tabBar is null)
            return;

        var tabs = Plugin.Config.Tabs;
        var signature = string.Join('\u0000', tabs.Select((tab, index) =>
            $"{tab.Name}#{(tab.PopOut ? 1 : 0)}#{GetUnreadSuffix(tab, index)}"));

        if (tabs.Count == renderedTabCount && signature == renderedTabListSignature)
            return;

        renderedTabCount = tabs.Count;
        renderedTabListSignature = signature;

        RebuildTabs();
    }

    private string GetUnreadSuffix(Tab tab, int index)
        => index == Plugin.LastTab || tab.UnreadMode == UnreadMode.None || tab.Unread == 0
            ? string.Empty
            : $" ({tab.Unread})";

    private void RebuildTabs()
    {
        if (tabBar is null)
            return;

        tabBar.Clear();

        string? selectedLabel = null;

        for (var i = 0; i < Plugin.Config.Tabs.Count; i++)
        {
            var index = i;
            var tab = Plugin.Config.Tabs[index];
            if (tab.PopOut)
                continue;

            var label = $"{tab.Name}{GetUnreadSuffix(tab, index)}";
            tabBar.AddTab(label, () => SwitchTab(index));

            if (index == Plugin.LastTab)
                selectedLabel = label;
        }

        if (selectedLabel is not null)
            tabBar.SelectTab(selectedLabel);
    }

    private void SwitchTab(int index)
    {
        if (index == Plugin.LastTab || index < 0 || index >= Plugin.Config.Tabs.Count)
            return;

        var previousTab = Plugin.CurrentTab;
        Plugin.LastTab = index;

        var tab = Plugin.Config.Tabs[index];
        if (tab.Channel is not null)
            tab.CurrentChannel.Channel = tab.Channel.Value;
        else if (tab.CurrentChannel.Channel is InputChannel.Invalid)
            tab.CurrentChannel = previousTab.CurrentChannel;

        Plugin.Functions.Chat.SetChannelWithExtraChat(tab.CurrentChannel.Channel);
        tab.Unread = 0;

        if (tabBar is not null)
        {
            renderedTabListSignature = string.Empty;
            tabBar.SelectTab($"{tab.Name}{GetUnreadSuffix(tab, index)}");
        }

        renderedTabIndex = -1;
        renderedTabName = string.Empty;
        LastActivityTime = Environment.TickCount64;
    }

    private void ShowTabMenu()
    {
        NativeCallback.Safe(() =>
        {
            var index = Plugin.LastTab;
            if (index < 0 || index >= Plugin.Config.Tabs.Count)
                return;

            Menu.Clear();
            Menu.AddItem(Language.Options_Tabs_Name, () => NativeCallback.Safe(() => OpenRenamePrompt(index), "renaming a tab"));
            Menu.AddItem(Language.ChatLog_Tabs_PopOut, () => NativeCallback.Safe(() => TogglePopOut(index), "popping out a tab"));
            Menu.AddItem(Language.Options_Tabs_NewTab, () => NativeCallback.Safe(AddTab, "adding a tab"));
            Menu.AddItem(Language.ChatLog_Tabs_OpenSettings, () => NativeCallback.Safe(OpenSettingsWindow, "opening the settings window"));

            OpenContextMenu();
        }, "opening the tab menu");
    }

    /// <summary>
    ///     The settings window is an addon, so it must not be created from inside a UI event callback.
    /// </summary>
    private void OpenSettingsWindow() =>
        Plugin.Framework.RunOnTick(() => NativeCallback.Safe(() => Plugin.NativeSettings.Open(), "opening the settings window"));

    private void TogglePopOut(int index)
    {
        if (index < 0 || index >= Plugin.Config.Tabs.Count)
            return;

        Plugin.Config.Tabs[index].PopOut = !Plugin.Config.Tabs[index].PopOut;
        renderedTabListSignature = string.Empty;
    }

    private void AddTab()
    {
        Plugin.Config.Tabs.Add(new Tab());
        Plugin.LastTab = Plugin.Config.Tabs.Count - 1;
        Plugin.Config.Tabs[Plugin.LastTab].Unread = 0;

        renderedTabListSignature = string.Empty;
        renderedTabIndex = -1;
    }

    private void OpenRenamePrompt(int index)
    {
        if (renamePrompt is not null || index < 0 || index >= Plugin.Config.Tabs.Count)
            return;

        var tab = Plugin.Config.Tabs[index];

        var prompt = new NativeTextPrompt(Plugin, tab.Name, result =>
        {
            renamePrompt = null;

            if (result is null)
                return;

            tab.Name = result;
            renderedTabListSignature = string.Empty;
        })
        {
            InternalName = "Chat2Ex_RenameTab",
            Title = Language.Options_Tabs_Name,
            Size = new Vector2(320.0f, 120.0f),
            DepthLayer = 13,
            DisableClose = true,
            DisableClamping = true,
        };

        prompt.Open();
        renamePrompt = prompt;
    }

    private void RebuildMessages(Tab tab)
    {
        if (messageScroll is null || renderer is null)
            return;

        var list = messageScroll.ContentNode;
        list.Clear();

        // MaxLinesToRender is reused as the amount of messages that can be visible at once.
        // The native renderer creates several nodes per message, so keep this bounded.
        var maxMessages = MaxRenderedMessages;

        pendingMessages.Clear();
        pendingMessageIndex = 0;
        pendingTab = tab;

        using (var messages = tab.Messages.GetReadOnly())
        {
            var start = Math.Max(0, messages.Count - maxMessages);
            for (var i = start; i < messages.Count; i++)
                pendingMessages.Add(messages[i]);
        }

        ProcessPendingMessages();
    }

    /// <summary>
    ///     Adds a limited batch of messages to the list each frame, avoiding a long hitch when a large
    ///     history has to be rebuilt.
    /// </summary>
    private void ProcessPendingMessages()
    {
        if (pendingTab is null || messageScroll is null || renderer is null)
            return;

        const int messagesPerFrame = 25;

        var list = messageScroll.ContentNode;
        var width = Math.Max(80.0f, list.Width - 12.0f); // keep clear of the scroll bar

        var batch = new List<KamiToolKit.BaseTypes.NodeBase>(messagesPerFrame);

        while (pendingMessageIndex < pendingMessages.Count && batch.Count < messagesPerFrame)
        {
            batch.Add(renderer.Build(pendingMessages[pendingMessageIndex], pendingTab, width, HandleLinkAction));
            pendingMessageIndex++;
        }

        if (batch.Count == 0)
        {
            pendingTab = null;
            return;
        }

        list.AddNode(batch);
        list.RecalculateLayout();
        messageScroll.RecalculateSizes();
        messageScroll.ScrollToEnd();

        if (pendingMessageIndex >= pendingMessages.Count)
            pendingTab = null;
    }

    public override void Dispose()
    {
        pendingMessages.Clear();
        pendingTab = null;

        renderer?.Dispose();
        renderer = null;

        // The menu is shared and outlives this window, only make sure the game side is cleared.
        Plugin.ChatUi.ContextMenu.Close();

        base.Dispose();
    }

    private void ShowMessageMenu(Message message)
    {
        Menu.Clear();
        Menu.AddItem(Language.Context_Copy, () => NativeCallback.Safe(() => CopyMessage(message, true), "copying a message"));

        if (message.Sender.Count > 0)
            Menu.AddItem(Language.Context_CopyContent, () => NativeCallback.Safe(() => CopyMessage(message, false), "copying a message"));

        Menu.AddItem(Language.Context_HideChat, () => NativeCallback.Safe(() => Plugin.ChatLog.CurrentHideState = HideState.User, "hiding the chat"));

        OpenContextMenu();
    }

    /// <summary>
    ///     Handles a click on a link or on a message row.
    /// </summary>
    /// <param name="message">The message that was clicked.</param>
    /// <param name="chunk">The link that was hit, or <c>null</c> when the row itself was clicked.</param>
    /// <param name="action">The kind of interaction.</param>
    private void HandleLinkAction(Message message, Chunk? chunk, LinkAction action)
    {
        if (action == LinkAction.Activate)
        {
            if (chunk is null)
                return;

            // Payloads with a left click action handle it themselves.
            if (NativePayloadHandler.TryHandleLeftClick(chunk))
                return;
        }

        // Player links get their own menu, like the game does for player names.
        if (chunk?.Link is PlayerPayload player)
        {
            ShowPlayerMenu(message, player);
            return;
        }

        if (action == LinkAction.Context)
            ShowMessageMenu(message);
    }

    /// <summary>
    ///     Shows the player related context menu for a player link.
    /// </summary>
    private void ShowPlayerMenu(Message message, PlayerPayload player)
    {
        NativeCallback.Safe(() =>
        {
            Menu.Clear();

            NativePlayerMenu.Build(Menu, Plugin, message, player, SetChatInput);

            Menu.AddItem(Language.Context_Copy, () => NativeCallback.Safe(() => CopyMessage(message, true), "copying a message"));

            if (message.Sender.Count > 0)
                Menu.AddItem(Language.Context_CopyContent, () => NativeCallback.Safe(() => CopyMessage(message, false), "copying a message"));

            Menu.AddItem(Language.Context_HideChat, () => NativeCallback.Safe(() => Plugin.ChatLog.CurrentHideState = HideState.User, "hiding the chat"));

            OpenContextMenu();
        }, "opening the player menu");
    }

    /// <summary>
    ///     Fills the chat input with the given text and focuses it.
    /// </summary>
    private void SetChatInput(string text)
    {
        if (IsMainWindow)
        {
            Plugin.ChatLog.ChatInput = text;
            Plugin.ChatLog.Activate = true;
            return;
        }

        if (inputNode is null)
            return;

        inputNode.String = text;
        inputNode.SetFocus();
    }

    private static void CopyMessage(Message message, bool withSender)
    {
        var builder = new StringBuilder();

        if (withSender)
            AppendChunks(builder, message.Sender);

        AppendChunks(builder, message.Content);

        ImGui.SetClipboardText(builder.ToString());
    }

    private static void AppendChunks(StringBuilder builder, IEnumerable<Chunk> chunks)
    {
        foreach (var chunk in chunks)
            if (chunk is TextChunk textChunk)
                builder.Append(textChunk.Content);
    }

    private void OnInputComplete(ReadOnlySeString text)
    {
        if (TargetTab.InputDisabled)
            return;

        var message = text.ToString().Trim();
        if (message.Length == 0)
            return;

        var tellSpecial = Plugin.ChatLog.TellSpecial;
        Plugin.ServerCore.SendHandler.SendChatBox(TargetTab, ref message, ref tellSpecial);
        Plugin.ChatLog.TellSpecial = tellSpecial;

        if (inputNode is not null)
            inputNode.String = string.Empty;

        if (IsMainWindow)
        {
            Plugin.ChatLog.ChatInput = string.Empty;
            renderedInput = string.Empty;
            Plugin.ChatUi.HideCommandHelp();
        }

        LastActivityTime = Environment.TickCount64;
    }

    private void OnInputReceived(ReadOnlySeString text)
    {
        var input = text.ToString();

        if (IsMainWindow)
        {
            Plugin.ChatLog.ChatInput = input;
            renderedInput = input;
            UpdateCommandHelp(input);
        }

        LastActivityTime = Environment.TickCount64;
    }

    /// <summary>
    ///     Mirrors the slash command help that used to be driven by the ImGui input callback.
    /// </summary>
    private void UpdateCommandHelp(string input)
    {
        if (!input.StartsWith('/'))
        {
            Plugin.ChatUi.HideCommandHelp();
            return;
        }

        var command = input.Split(' ')[0];

        if (Plugin.Commands.AllCommands.TryGetValue(command, out var textCommand))
        {
            Plugin.ChatUi.ShowCommandHelp(textCommand.Description);
            return;
        }

        if (Plugin.CommandManager.Commands.TryGetValue(command, out var info) && info.ShowInHelp)
        {
            Plugin.ChatUi.ShowCommandHelp(info.HelpMessage);
            return;
        }

        Plugin.ChatUi.HideCommandHelp();
    }

    private void OnInputFocused()
    {
        if (!IsMainWindow)
        {
            LastActivityTime = Environment.TickCount64;
            return;
        }

        var handler = Plugin.ChatLog;
        handler.Activate = true;
        handler.LastActivityTime = Environment.TickCount64;
        LastActivityTime = Environment.TickCount64;
    }

    private void OnInputFocusLost()
    {
        if (!IsMainWindow)
            return;

        Plugin.ChatLog.Activate = false;
        Plugin.ChatUi.HideCommandHelp();
    }
}
