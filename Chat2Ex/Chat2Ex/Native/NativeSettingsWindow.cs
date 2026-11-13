using System.Globalization;
using System.Numerics;
using Chat2Ex.Code;
using Chat2Ex.Resources;
using Chat2Ex.Util;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Addons;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using ModifierFlag = Chat2Ex.GameFunctions.Types.ModifierFlag;

namespace Chat2Ex.Native;

/// <summary>
///     Native settings window. Tabs are ported one by one; tabs that are not ported yet show a placeholder.
/// </summary>
public sealed class NativeSettingsWindow : NativeAddon
{
    private const float SidebarWidth = 150.0f;
    private const float FooterHeight = 34.0f;
    private const float SidebarButtonHeight = 26.0f;

    private static readonly Vector4 DescriptionColor = new(0.72f, 0.72f, 0.72f, 1.0f);

    private readonly Plugin Plugin;
    private readonly Action onRequestClose;

    private Configuration mutable = null!;

    private VerticalListNode? sidebar;
    private ScrollingNode<ResNode>? contentScroll;
    private TextNode? measureNode;
    private readonly List<NodeBase> contentNodes = [];
    private TextButtonNode? saveButton;
    private TextButtonNode? saveAndCloseButton;
    private TextButtonNode? discardButton;

    private int selectedTab;
    private bool needsContentRebuild;
    private bool needsSidebarRebuild;

    private Action<ConfigKeyBind?>? keybindCapture;
    private readonly HashSet<VirtualKey> lastPressedKeys = [];

    public NativeSettingsWindow(Plugin plugin, Action onRequestClose)
    {
        Plugin = plugin;
        this.onRequestClose = onRequestClose;

        mutable = new Configuration();
        mutable.UpdateFrom(Plugin.Config, false);
    }

    private float ContentWidth => Math.Max(100.0f, (contentScroll?.Width ?? 400.0f) - 12.0f);

    protected override unsafe void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        var start = ContentStartPosition;
        var size = ContentSize;

        // Used to measure unwrapped text widths; must not have word wrap enabled.
        measureNode = new TextNode
        {
            FontSize = 11,
            LineSpacing = 14,
        };

        sidebar = new VerticalListNode
        {
            Position = start,
            Size = new Vector2(SidebarWidth, size.Y - FooterHeight),
            ItemSpacing = 2.0f,
        };
        sidebar.AttachNode(this);
        RebuildSidebar();

        contentScroll = new ScrollingNode<ResNode>
        {
            Position = new Vector2(start.X + SidebarWidth + 8.0f, start.Y),
            Size = new Vector2(size.X - SidebarWidth - 8.0f, size.Y - FooterHeight),
            AutoHideScrollBar = true,
        };
        contentScroll.AttachNode(this);
        contentScroll.RecalculateSizes();

        var footerY = start.Y + size.Y - 28.0f;
        saveButton = new TextButtonNode
        {
            Position = new Vector2(start.X, footerY),
            Size = new Vector2(110.0f, 28.0f),
            String = Language.Settings_Save,
            OnClick = () => Save(false),
        };
        saveButton.AttachNode(this);

        saveAndCloseButton = new TextButtonNode
        {
            Position = new Vector2(start.X + 118.0f, footerY),
            Size = new Vector2(150.0f, 28.0f),
            String = Language.Settings_SaveAndClose,
            OnClick = () => Save(true),
        };
        saveAndCloseButton.AttachNode(this);

        discardButton = new TextButtonNode
        {
            Position = new Vector2(start.X + 276.0f, footerY),
            Size = new Vector2(110.0f, 28.0f),
            String = Language.Settings_Discard,
            OnClick = CloseSettings,
        };
        discardButton.AttachNode(this);

        RebuildContent();
    }

    protected override unsafe void OnDraw(AtkUnitBase* addon)
    {
        UpdateKeybindCapture();

        if (needsSidebarRebuild)
        {
            needsSidebarRebuild = false;
            RebuildSidebar();
        }

        if (needsContentRebuild)
        {
            needsContentRebuild = false;
            RebuildContent();
        }

        base.OnDraw(addon);
    }

    protected override unsafe void OnFinalize(AtkUnitBase* addon)
    {
        sidebar = null;
        contentScroll = null;
        saveButton = null;
        saveAndCloseButton = null;
        discardButton = null;

        base.OnFinalize(addon);
    }

    private static string[] TabNames() =>
    [
        Language.Options_Display_Tab,
        Language.Options_ChatLog_Tab,
        Language.Options_Emote_Tab,
        Language.Options_Preview_Tab,
        Language.Options_Fonts_Tab,
        Language.Options_ChatColours_Tab,
        Language.Options_Tabs_Tab,
        Language.Options_Database_Tab,
        Language.Options_Webinterface_Tab,
        Language.Options_Miscellaneous_Tab,
        Language.Options_Changelog_Tab,
        Language.Options_About_Tab,
    ];

    private void RebuildSidebar()
    {
        if (sidebar is null)
            return;

        sidebar.Clear();

        var names = TabNames();
        for (var i = 0; i < names.Length; i++)
        {
            var index = i;
            var selected = index == selectedTab;

            sidebar.AddNode(new TextButtonNode
            {
                Size = new Vector2(SidebarWidth, SidebarButtonHeight),
                String = selected ? $"> {names[index]}" : names[index],
                OnClick = () => SelectTab(index),
            });
        }
    }

    private void SelectTab(int index)
    {
        if (index == selectedTab)
            return;

        selectedTab = index;
        needsSidebarRebuild = true;
        needsContentRebuild = true;
    }

    private void RebuildContent()
    {
        if (contentScroll is null)
            return;

        foreach (var node in contentNodes)
            node.Dispose();

        contentNodes.Clear();

        var list = new SettingsList(contentScroll.ContentNode, ContentWidth, contentNodes);

        switch (selectedTab)
        {
            case 0:
                BuildDisplayTab(list);
                break;
            case 1:
                BuildChatLogTab(list);
                break;
            case 2:
                AddDescription(list,
                    "Emote image replacement is not available in the native UI yet. Blocked emotes keep working for the web interface and for messages that are exported.");
                AddText(list, string.Format("{0}: {1}", Language.Options_Emote_BlockedEmotes, mutable.BlockedEmotes.Count));
                break;
            case 3:
                BuildPreviewTab(list);
                break;
            case 4:
                BuildFontsTab(list);
                break;
            case 5:
                BuildChatColoursTab(list);
                break;
            case 6:
                BuildTabsTab(list);
                break;
            case 7:
                BuildDatabaseTab(list);
                break;
            case 8:
                BuildWebinterfaceTab(list);
                break;
            case 9:
                BuildMiscellaneousTab(list);
                break;
            case 10:
                BuildChangelogTab(list);
                break;
            case 11:
                BuildAboutTab(list);
                break;
            default:
                AddDescription(list, "This settings tab has not been ported to the native UI yet.");
                break;
        }

        list.Finish();
        contentScroll.RecalculateSizes();
        contentScroll.ScrollToStart();
    }

    /// <summary>
    ///     Minimal manual layout container. Native layout lists did not reliably report the height of
    ///     component nodes (checkboxes ended up with zero height), so rows are positioned manually.
    /// </summary>
    private sealed class SettingsList(ResNode content, float width, List<NodeBase> nodes)
    {
        private float y;

        public void AddNode(NodeBase? node)
        {
            if (node is null)
                return;

            node.Position = new Vector2(0.0f, y);
            node.AttachNode(content);

            nodes.Add(node);
            y += Math.Max(1.0f, node.Height) + 2.0f;
        }

        public void AddNode(IEnumerable<NodeBase> batch)
        {
            foreach (var node in batch)
                AddNode(node);
        }

        public void Finish() => content.Size = new Vector2(width, Math.Max(1.0f, y));
    }

    /// <summary>
    ///     Font family cannot be changed (the native UI always renders with the game font), but the size
    ///     used by the chat window is configurable.
    /// </summary>
    private void BuildFontsTab(SettingsList list)
    {
        AddDescription(list,
            "原生界面固定使用游戏字体，无法更换字体族；字号可调。 / The native UI always uses the game font, only the size is configurable.");

        AddIntOption(list,
            Language.Options_FontSize_Name,
            null,
            () => mutable.ChatFontSize,
            value => mutable.ChatFontSize = Math.Clamp(value, 10, 24),
            10,
            24);

        // Preview of the configured size using a sample line.
        var preview = new TextNode
        {
            FontSize = (uint)Math.Clamp(mutable.ChatFontSize, 10, 24),
            LineSpacing = (uint)Math.Clamp(mutable.ChatFontSize, 10, 24) + 3,
            TextColor = new Vector4(0.9f, 0.9f, 0.9f, 1.0f),
            TextOutlineColor = new Vector4(0.0f, 0.0f, 0.0f, 0.85f),
            String = "示例文本 Sample text 0123456789",
        };

        preview.Height = preview.FontSize + 6.0f;
        preview.Width = ContentWidth;

        list.AddNode(preview);
    }

    private void BuildDisplayTab(SettingsList list)
    {
        AddOption(list,
            Language.Options_HideChat_Name,
            Language.Options_HideChat_Description,
            () => mutable.HideChat,
            value => mutable.HideChat = value);

        AddOption(list,
            Language.Options_HideDuringCutscenes_Name,
            Language.Options_HideDuringCutscenes_Description,
            () => mutable.HideDuringCutscenes,
            value => mutable.HideDuringCutscenes = value);

        AddOption(list,
            Language.Options_HideWhenNotLoggedIn_Name,
            Language.Options_HideWhenNotLoggedIn_Description,
            () => mutable.HideWhenNotLoggedIn,
            value => mutable.HideWhenNotLoggedIn = value);

        AddOption(list,
            Language.Options_HideWhenUiHidden_Name,
            Language.Options_HideWhenUiHidden_Description,
            () => mutable.HideWhenUiHidden,
            value => mutable.HideWhenUiHidden = value);

        AddOption(list,
            Language.Options_HideInLoadingScreens_Name,
            Language.Options_HideInLoadingScreens_Description,
            () => mutable.HideInLoadingScreens,
            value => mutable.HideInLoadingScreens = value);

        AddOption(list,
            Language.Options_HideInBattle_Name,
            Language.Options_HideInBattle_Description,
            () => mutable.HideInBattle,
            value => mutable.HideInBattle = value);

        AddOption(list,
            Language.Options_HideWhenInactive_Name,
            Language.Options_HideWhenInactive_Description,
            () => mutable.HideWhenInactive,
            value => mutable.HideWhenInactive = value);

        if (mutable.HideWhenInactive)
        {
            AddIntOption(list,
                Language.Options_InactivityHideTimeout_Name,
                Language.Options_InactivityHideTimeout_Description,
                () => mutable.InactivityHideTimeout,
                value => mutable.InactivityHideTimeout = Math.Max(2, value),
                2,
                3600);

            AddOption(list,
                Language.Options_InactivityHideActiveDuringBattle_Name,
                Language.Options_InactivityHideActiveDuringBattle_Description,
                () => mutable.InactivityHideActiveDuringBattle,
                value => mutable.InactivityHideActiveDuringBattle = value);
        }

        AddOption(list,
            Language.Options_Use24HourClock_Name,
            Language.Options_Use24HourClock_Description,
            () => mutable.Use24HourClock,
            value => mutable.Use24HourClock = value);

        AddOption(list,
            Language.Options_PrettierTimestamps_Name,
            Language.Options_PrettierTimestamps_Description,
            () => mutable.PrettierTimestamps,
            value => mutable.PrettierTimestamps = value);

        if (mutable.PrettierTimestamps)
        {
            AddOption(list,
                Language.Options_MoreCompactPretty_Name,
                Language.Options_MoreCompactPretty_Description,
                () => mutable.MoreCompactPretty,
                value => mutable.MoreCompactPretty = value);

            AddOption(list,
                Language.Options_HideSameTimestamps_Name,
                Language.Options_HideSameTimestamps_Description,
                () => mutable.HideSameTimestamps,
                value => mutable.HideSameTimestamps = value);
        }

        AddOption(list,
            Language.Options_CollapseDuplicateMessages_Name,
            Language.Options_CollapseDuplicateMessages_Description,
            () => mutable.CollapseDuplicateMessages,
            value => mutable.CollapseDuplicateMessages = value);

        if (mutable.CollapseDuplicateMessages)
        {
            AddOption(list,
                Language.Options_CollapseDuplicateMsgUniqueLink_Name,
                Language.Options_CollapseDuplicateMsgUniqueLink_Description,
                () => mutable.CollapseKeepUniqueLinks,
                value => mutable.CollapseKeepUniqueLinks = value);
        }
    }

    private void BuildChatLogTab(SettingsList list)
    {
        AddOption(list,
            Language.Options_PlaySounds_Name,
            Language.Options_PlaySounds_Description,
            () => mutable.PlaySounds,
            value => mutable.PlaySounds = value);

        AddOption(list,
            Language.Options_NativeItemTooltips_Name,
            string.Format(Language.Options_NativeItemTooltips_Description, Plugin.PluginName),
            () => mutable.NativeItemTooltips,
            value => mutable.NativeItemTooltips = value);

        AddFloatOption(list,
            Language.Options_WindowOpacity_Name,
            null,
            () => mutable.WindowAlpha,
            value => mutable.WindowAlpha = value,
            0.0f,
            100.0f);

        AddFloatOption(list,
            "聊天背景透明度 / Background opacity",
            null,
            () => mutable.ChatBackgroundOpacity,
            value => mutable.ChatBackgroundOpacity = value,
            0.0f,
            100.0f);

        AddIntOption(list,
            Language.Options_MaxLinesToShow_Name,
            Language.Options_MaxLinesToShow_Description,
            () => mutable.MaxLinesToRender,
            value => mutable.MaxLinesToRender = Math.Clamp(value, 1, 10_000),
            1,
            10_000);

        AddKeybindOption(list,
            Language.Options_ChatTabForwardKeybind_Name,
            () => mutable.ChatTabForward,
            value => mutable.ChatTabForward = value);

        AddKeybindOption(list,
            Language.Options_ChatTabBackwardKeybind_Name,
            () => mutable.ChatTabBackward,
            value => mutable.ChatTabBackward = value);

        if (keybindCapture is not null)
            AddDescription(list, Language.Keybind_EscToClear);

        AddOption(list,
            Language.Options_LockWindowPosition_Name,
            Language.Options_LockWindowPosition_Description,
            () => mutable.LockWindowPosition,
            value => mutable.LockWindowPosition = value);

        if (!mutable.LockWindowPosition)
            AddPositionOption(list);
    }

    private void AddFloatOption(SettingsList list, string label, string? description, Func<float> get, Action<float> set, float min, float max)
    {
        list.AddNode(new TextNode
        {
            Size = new Vector2(ContentWidth, 16.0f),
            FontSize = 12,
            LineSpacing = 15,
            String = label,
        });

        var slider = new FloatSliderNode
        {
            Size = new Vector2(ContentWidth, 24.0f),
            Min = min,
            Max = max,
            Step = 0.25f,
            Value = Math.Clamp(get(), min, max),
        };

        slider.OnValueChanged = value => set(value);

        list.AddNode(slider);

        if (!string.IsNullOrEmpty(description))
            AddDescription(list, description);
    }

    private void AddKeybindOption(SettingsList list, string label, Func<ConfigKeyBind?> get, Action<ConfigKeyBind?> set)
    {
        list.AddNode(new TextNode
        {
            Size = new Vector2(ContentWidth, 16.0f),
            FontSize = 12,
            LineSpacing = 15,
            String = label,
        });

        var current = get();
        var capturing = keybindCapture is not null && keybindCapture.Equals(set);

        var button = new TextButtonNode
        {
            Size = new Vector2(ContentWidth, 26.0f),
            String = capturing ? "..." : current?.ToString() ?? $"({Language.Keybind_None})",
            OnClick = () =>
            {
                keybindCapture = set;
                lastPressedKeys.Clear();
                needsContentRebuild = true;
            },
        };

        list.AddNode(button);
    }

    private void AddPositionOption(SettingsList list)
    {
        var position = Plugin.ChatUi.MainWindowPosition ?? Vector2.Zero;

        list.AddNode(new TextNode
        {
            Size = new Vector2(ContentWidth, 16.0f),
            FontSize = 12,
            LineSpacing = 15,
            String = Language.Options_AdjustPosition_Name,
        });

        var xInput = new NumericInputNode
        {
            Size = new Vector2(90.0f, 24.0f),
            Min = 0,
            Max = 5000,
            Value = (int)position.X,
        };
        xInput.OnValueUpdate = value =>
        {
            var target = Plugin.ChatUi.MainWindowPosition ?? Vector2.Zero;
            Plugin.ChatUi.MoveMainWindow(new Vector2(value, target.Y));
        };
        list.AddNode(xInput);

        var yInput = new NumericInputNode
        {
            Size = new Vector2(90.0f, 24.0f),
            Min = 0,
            Max = 5000,
            Value = (int)position.Y,
        };
        yInput.OnValueUpdate = value =>
        {
            var target = Plugin.ChatUi.MainWindowPosition ?? Vector2.Zero;
            Plugin.ChatUi.MoveMainWindow(new Vector2(target.X, value));
        };
        list.AddNode(yInput);

        AddDescription(list, Language.Options_AdjustPosition_Warning);
    }

    private void UpdateKeybindCapture()
    {
        if (keybindCapture is null)
        {
            lastPressedKeys.Clear();
            return;
        }

        var pressed = new HashSet<VirtualKey>();
        foreach (var key in Enum.GetValues<VirtualKey>())
        {
            if (key is VirtualKey.NO_KEY)
                continue;

            if (Plugin.KeyState[key])
                pressed.Add(key);
        }

        var newKeys = pressed.Except(lastPressedKeys).ToArray();
        lastPressedKeys.Clear();
        foreach (var key in pressed)
            lastPressedKeys.Add(key);

        if (newKeys.Length == 0)
            return;

        if (newKeys.Contains(VirtualKey.ESCAPE))
        {
            FinishKeybindCapture(null);
            return;
        }

        foreach (var key in newKeys)
        {
            if (key is VirtualKey.CONTROL or VirtualKey.LCONTROL or VirtualKey.RCONTROL
                or VirtualKey.SHIFT or VirtualKey.LSHIFT or VirtualKey.RSHIFT
                or VirtualKey.MENU or VirtualKey.LMENU or VirtualKey.RMENU)
                continue;

            var modifiers = ModifierFlag.None;

            if (Plugin.KeyState[VirtualKey.CONTROL] || Plugin.KeyState[VirtualKey.LCONTROL] || Plugin.KeyState[VirtualKey.RCONTROL])
                modifiers |= ModifierFlag.Ctrl;

            if (Plugin.KeyState[VirtualKey.SHIFT] || Plugin.KeyState[VirtualKey.LSHIFT] || Plugin.KeyState[VirtualKey.RSHIFT])
                modifiers |= ModifierFlag.Shift;

            if (Plugin.KeyState[VirtualKey.MENU] || Plugin.KeyState[VirtualKey.LMENU] || Plugin.KeyState[VirtualKey.RMENU])
                modifiers |= ModifierFlag.Alt;

            FinishKeybindCapture(new ConfigKeyBind { Modifier = modifiers, Key = key });
            return;
        }
    }

    private void FinishKeybindCapture(ConfigKeyBind? keybind)
    {
        var callback = keybindCapture;
        keybindCapture = null;
        lastPressedKeys.Clear();

        callback?.Invoke(keybind);
        needsContentRebuild = true;
    }

    private long databaseRefreshTicks;
    private long databaseSize;
    private long databaseLogSize;
    private int databaseMessageCount;

    private void BuildDatabaseTab(SettingsList list)
    {
        AddOption(list,
            Language.Options_DatabaseBattleMessages_Name,
            Language.Options_DatabaseBattleMessages_Description,
            () => mutable.DatabaseBattleMessages,
            value => mutable.DatabaseBattleMessages = value);

        AddOption(list,
            Language.Options_DatabaseGatherCraftMessages_Name,
            Language.Options_DatabaseGatherCraftMessages_Description,
            () => mutable.DatabaseGatherCraftMessages,
            value => mutable.DatabaseGatherCraftMessages = value);

        AddOption(list,
            Language.Options_LoadPreviousSession_Name,
            Language.Options_LoadPreviousSession_Description,
            () => mutable.LoadPreviousSession,
            value =>
            {
                mutable.LoadPreviousSession = value;
                if (value)
                    mutable.FilterIncludePreviousSessions = true;
            });

        AddOption(list,
            Language.Options_FilterIncludePreviousSessions_Name,
            Language.Options_FilterIncludePreviousSessions_Description,
            () => mutable.FilterIncludePreviousSessions,
            value =>
            {
                mutable.FilterIncludePreviousSessions = value;
                if (!value)
                    mutable.LoadPreviousSession = false;
            });

        var configDirectory = Plugin.Interface.ConfigDirectory.FullName;
        var oldDatabase = new FileInfo(Path.Join(configDirectory, "chat.db"));
        var migratedOldDatabase = new FileInfo(Path.Join(configDirectory, "chat-litedb.db"));

        if (oldDatabase.Exists || migratedOldDatabase.Exists)
        {
            AddDescription(list, Language.Options_Database_Old_Heading);

            AddButtonRow(list, (Language.Options_Database_Old_Delete, () =>
            {
                try
                {
                    if (oldDatabase.Exists)
                        oldDatabase.Delete();
                    else
                        migratedOldDatabase.Delete();

                    WrapperUtil.AddNotification(Language.Options_Database_Old_Delete_Success, NotificationType.Success);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Unable to delete old database");
                    WrapperUtil.AddNotification(Language.Options_Database_Old_Delete_Error, NotificationType.Error);
                }
            }));
        }

        AddDescription(list, Language.Options_Database_Metadata_Heading);

        if (databaseRefreshTicks + 5000 < Environment.TickCount64)
        {
            databaseSize = Plugin.MessageManager.Store.DatabaseSize();
            databaseLogSize = Plugin.MessageManager.Store.DatabaseLogSize();
            databaseMessageCount = Plugin.MessageManager.Store.MessageCount();
            databaseRefreshTicks = Environment.TickCount64;
        }

        AddDescription(list, string.Format(Language.Options_Database_Metadata_Path, MessageManager.DatabasePath()));
        AddDescription(list, string.Format(Language.Options_Database_Metadata_Size, StringUtil.BytesToString(databaseSize)));
        AddDescription(list, string.Format(Language.Options_Database_Metadata_LogSize, StringUtil.BytesToString(databaseLogSize)));
        AddDescription(list, string.Format(Language.Options_Database_Metadata_MessageCount, databaseMessageCount));

        AddButtonRow(list,
            (Language.Options_Database_Metadata_CopyConfigPath, () =>
            {
                var directory = Path.GetDirectoryName(MessageManager.DatabasePath());
                if (directory is not null)
                    Dalamud.Bindings.ImGui.ImGui.SetClipboardText(directory);
            }),
            (Language.Options_ClearDatabase_Button, () =>
            {
                Plugin.Log.Warning("Clearing messages from database");
                Plugin.MessageManager.Store.ClearMessages();
                Plugin.MessageManager.ClearAllTabs();

                databaseRefreshTicks = 0;
                WrapperUtil.AddNotification(Language.Options_ClearDatabase_Success, NotificationType.Info);
                needsContentRebuild = true;
            }));
    }

    private void BuildPreviewTab(SettingsList list)
    {
        AddDropDown(list,
            Language.Options_Preview_Name,
            Language.Options_Preview_Description,
            mutable.PreviewPosition,
            Enum.GetValues<PreviewPosition>(),
            value => value.Name(),
            value => mutable.PreviewPosition = value);

        AddIntOption(list,
            Language.Options_PreviewMinimum_Name,
            Language.Options_PreviewMinimum_Description,
            () => mutable.PreviewMinimum,
            value => mutable.PreviewMinimum = Math.Clamp(value, 1, 250),
            1,
            250);

        AddOption(list,
            Language.Options_PreviewOnlyIf_Name,
            Language.Options_PreviewOnlyIf_Description,
            () => mutable.OnlyPreviewIf,
            value => mutable.OnlyPreviewIf = value);
    }

    private void BuildWebinterfaceTab(SettingsList list)
    {
        AddDescription(list, Language.Options_Webinterface_Warning_Header);
        AddDescription(list, Language.Options_Webinterface_Warning_Reason);
        AddDescription(list, Language.Options_Webinterface_DoNot_Port);
        AddDescription(list, Language.Options_Webinterface_DoNot_Share);
        AddDescription(list, Language.Options_Webinterface_DoNot_Multibox);
        AddDescription(list, Language.Options_Webinterface_Warning_Support);

        AddOption(list,
            Language.Options_WebinterfaceEnable_Name,
            Language.Options_WebinterfaceEnable_Description,
            () => mutable.WebinterfaceEnabled,
            value => mutable.WebinterfaceEnabled = value);

        if (!mutable.WebinterfaceEnabled)
            return;

        AddOption(list,
            Language.Options_WebinterfaceAutoStart_Name,
            Language.Options_WebinterfaceAutoStart_Description,
            () => mutable.WebinterfaceAutoStart,
            value => mutable.WebinterfaceAutoStart = value);

        AddIntOption(list,
            Language.Webinterface_Option_Port_Name,
            Language.Webinterface_Option_Port_Description,
            () => mutable.WebinterfacePort,
            value => mutable.WebinterfacePort = Math.Clamp(value, 1024, 49151),
            1024,
            49151);

        AddIntOption(list,
            Language.Options_WebinterfaceMaxLinesToSend_Name,
            Language.Options_WebinterfaceMaxLinesToSend_Description,
            () => mutable.WebinterfaceMaxLinesToSend,
            value => mutable.WebinterfaceMaxLinesToSend = Math.Clamp(value, 1, 10_000),
            1,
            10_000);

        AddDescription(list, Language.Webinterface_CurrentPassword);
        AddText(list, mutable.WebinterfacePassword);

        AddButtonRow(list,
            (Language.Webinterface_PasswordReset_Tooltip, () =>
            {
                mutable.WebinterfacePassword = WebinterfaceUtil.GenerateSimpleAuthCode();
                Plugin.ServerCore.InvalidateSessions();
                needsContentRebuild = true;
            }));

        AddDescription(list, Language.Webinterface_Controls);

        var isActive = Plugin.ServerCore.IsActive();

        AddButtonRow(list,
            (Language.Webinterface_Button_Start, () =>
            {
                if (Plugin.ServerCore.IsActive() || Plugin.ServerCore.IsStopping())
                    return;

                _ = Task.Run(() =>
                {
                    if (Plugin.ServerCore.Start())
                        Plugin.ServerCore.Run();
                });
            }),
            (Language.Webinterface_Button_Stop, () =>
            {
                if (!Plugin.ServerCore.IsActive() || Plugin.ServerCore.IsStopping())
                    return;

                _ = Task.Run(async () => await Plugin.ServerCore.Stop());
            }));

        AddText(list, $"{Language.Webinterface_Controls_Active}: {(isActive ? "ON" : "OFF")}");
    }

    private void BuildChatColoursTab(SettingsList list)
    {
        foreach (var (_, types) in ChatTypeExt.SortOrder)
        {
            foreach (var type in types)
                AddColorRow(list, type);
        }
    }

    private void AddColorRow(SettingsList list, ChatType type)
    {
        var current = mutable.ChatColours.TryGetValue(type, out var stored)
            ? ColourUtil.RgbaToVector4(stored) ?? Vector4.One
            : ColourUtil.RgbaToVector4(type.DefaultColor() ?? 0xFFFFFFFF) ?? Vector4.One;

        var spacing = 6.0f;
        var resetWidth = 72.0f;
        var importWidth = 88.0f;
        var nameWidth = Math.Max(90.0f, ContentWidth - 26.0f - resetWidth - importWidth - spacing * 3.0f);

        var row = new HorizontalListNode
        {
            Size = new Vector2(ContentWidth, 26.0f),
            ItemSpacing = spacing,
        };

        row.Height = 26.0f;
        row.Width = ContentWidth;

        row.AddNode(new ColorImageNode
        {
            Size = new Vector2(24.0f, 22.0f),
            Color = current,
        });

        row.AddNode(new TextButtonNode
        {
            Size = new Vector2(nameWidth, 26.0f),
            String = type.Name(),
            OnClick = () => OpenColorPicker(type, current),
        });

        row.AddNode(new TextButtonNode
        {
            Size = new Vector2(resetWidth, 26.0f),
            String = Language.Options_ChatColours_Reset,
            OnClick = () =>
            {
                mutable.ChatColours.Remove(type);
                needsContentRebuild = true;
            },
        });

        row.AddNode(new TextButtonNode
        {
            Size = new Vector2(importWidth, 26.0f),
            String = Language.Options_ChatColours_Import,
            OnClick = () =>
            {
                mutable.ChatColours[type] = Plugin.Functions.Chat.GetChannelColor(type) ?? type.DefaultColor() ?? 0;
                needsContentRebuild = true;
            },
        });

        list.AddNode(row);
    }

    private void OpenColorPicker(ChatType type, Vector4 current)
    {
        if (colorPicker is not null)
            return;

        var picker = new ColorPickerAddon
        {
            InternalName = "Chat2Ex_ColorPicker",
            Title = type.Name(),
            Size = new Vector2(340.0f, 420.0f),
            DepthLayer = 13,
            DisableClamping = true,
            InitialColor = current,
            DefaultColor = ColourUtil.RgbaToVector4(type.DefaultColor() ?? 0xFFFFFFFF),
        };

        picker.OnColorConfirmed = color =>
        {
            mutable.ChatColours[type] = ColourUtil.ComponentsToRgba(
                (byte)Math.Round(color.X * 255.0f),
                (byte)Math.Round(color.Y * 255.0f),
                (byte)Math.Round(color.Z * 255.0f),
                (byte)Math.Round(color.W * 255.0f));

            colorPicker = null;
            needsContentRebuild = true;
        };

        picker.OnColorCancelled = () => colorPicker = null;

        picker.Open();
        colorPicker = picker;
    }

    private ColorPickerAddon? colorPicker;

    private void BuildTabsTab(SettingsList list)
    {
        var tabs = mutable.Tabs;
        if (tabs.Count == 0)
        {
            AddDescription(list, "No tabs configured.");
            return;
        }

        editingTabIndex = Math.Clamp(editingTabIndex, 0, tabs.Count - 1);
        var tab = tabs[editingTabIndex];

        var tabNames = tabs.Select((entry, index) => $"{index + 1}. {entry.Name}").ToList();
        AddDropDown(list,
            Language.Options_Tabs_Tab,
            null,
            tabNames[editingTabIndex],
            tabNames,
            value => value,
            value =>
            {
                var index = tabNames.IndexOf(value);
                if (index < 0)
                    return;

                editingTabIndex = index;
                needsContentRebuild = true;
            });

        AddButtonRow(list,
            (Language.Options_Tabs_NewTab, () =>
            {
                mutable.Tabs.Add(new Tab());
                editingTabIndex = mutable.Tabs.Count - 1;
                needsContentRebuild = true;
            }),
            (Language.Options_Tabs_Name, () => OpenRenamePrompt(editingTabIndex)));

        AddButtonRow(list,
            (Language.ChatLog_Tabs_MoveUp, () => MoveTab(-1)),
            (Language.ChatLog_Tabs_MoveDown, () => MoveTab(1)),
            (Language.Options_Tabs_Delete, () => DeleteTab()));

        AddOption(list, Language.Options_Tabs_ShowTimestamps, null,
            () => tab.DisplayTimestamp, value => tab.DisplayTimestamp = value);

        AddOption(list, Language.Options_Tabs_PopOut, null,
            () => tab.PopOut, value => tab.PopOut = value);

        if (tab.PopOut)
        {
            AddOption(list, Language.Options_Tabs_IndependentOpacity, null,
                () => tab.IndependentOpacity, value => tab.IndependentOpacity = value);

            if (tab.IndependentOpacity)
            {
                AddFloatOption(list, Language.Options_Tabs_Opacity, null,
                    () => tab.Opacity, value => tab.Opacity = value, 0.0f, 100.0f);
            }

            AddOption(list, Language.Options_Tabs_IndependentHide, null,
                () => tab.IndependentHide, value => tab.IndependentHide = value);

            if (tab.IndependentHide)
            {
                AddOption(list, Language.Options_HideDuringCutscenes_Name, null,
                    () => tab.HideDuringCutscenes, value => tab.HideDuringCutscenes = value);
                AddOption(list, Language.Options_HideWhenNotLoggedIn_Name, null,
                    () => tab.HideWhenNotLoggedIn, value => tab.HideWhenNotLoggedIn = value);
                AddOption(list, Language.Options_HideWhenUiHidden_Name, null,
                    () => tab.HideWhenUiHidden, value => tab.HideWhenUiHidden = value);
                AddOption(list, Language.Options_HideInLoadingScreens_Name, null,
                    () => tab.HideInLoadingScreens, value => tab.HideInLoadingScreens = value);
                AddOption(list, Language.Options_HideInBattle_Name, null,
                    () => tab.HideInBattle, value => tab.HideInBattle = value);
            }
        }

        AddDropDown(list,
            Language.Options_Tabs_UnreadMode,
            null,
            tab.UnreadMode,
            Enum.GetValues<UnreadMode>(),
            value => value.Name(),
            value => tab.UnreadMode = value);

        if (mutable.HideWhenInactive)
        {
            AddOption(list, Language.Options_Tabs_InactivityBehaviour, null,
                () => tab.UnhideOnActivity, value => tab.UnhideOnActivity = value);
        }

        AddOption(list, Language.Options_Tabs_NoInput, null,
            () => tab.InputDisabled, value => tab.InputDisabled = value);

        if (!tab.InputDisabled)
        {
            var channelNames = new List<string> { Language.Options_Tabs_NoInputChannel };
            channelNames.AddRange(Enum.GetValues<InputChannel>().Select(channel => channel.ToChatType().Name()));

            var currentName = tab.Channel?.ToChatType().Name() ?? Language.Options_Tabs_NoInputChannel;

            AddDropDown(list,
                Language.Options_Tabs_InputChannel,
                null,
                currentName,
                channelNames,
                value => value,
                value =>
                {
                    if (value == Language.Options_Tabs_NoInputChannel)
                    {
                        tab.Channel = null;
                        return;
                    }

                    foreach (var channel in Enum.GetValues<InputChannel>())
                    {
                        if (channel.ToChatType().Name() != value)
                            continue;

                        tab.Channel = channel;
                        break;
                    }
                });
        }

        AddDescription(list, Language.Options_Tabs_Channels);

        foreach (var (header, types) in ChatTypeExt.SortOrder)
        {
            AddDescription(list, header);

            AddButtonRow(list,
                (Language.ChannelSelector_Select, () =>
                {
                    foreach (var type in types)
                        tab.SelectedChannels.TryAdd(type, (ChatSourceExt.All, ChatSourceExt.All));

                    needsContentRebuild = true;
                }),
                (Language.ChannelSelector_Unselect, () =>
                {
                    foreach (var type in types)
                        tab.SelectedChannels.Remove(type);

                    needsContentRebuild = true;
                }));

            foreach (var type in types)
            {
                var channelType = type;
                var checkbox = new CheckboxNode
                {
                    String = channelType.Name(),
                    IsChecked = tab.SelectedChannels.ContainsKey(channelType),
                };

                checkbox.OnClick = value =>
                {
                    if (value)
                        tab.SelectedChannels[channelType] = (ChatSourceExt.All, ChatSourceExt.All);
                    else
                        tab.SelectedChannels.Remove(channelType);
                };

                // Component nodes report a zero height until it is set explicitly.
                checkbox.Height = 22.0f;
                checkbox.Width = ContentWidth;

                list.AddNode(checkbox);
            }
        }
    }

    private int editingTabIndex;

    private void MoveTab(int offset)
    {
        var target = editingTabIndex + offset;
        if (target < 0 || target >= mutable.Tabs.Count)
            return;

        (mutable.Tabs[editingTabIndex], mutable.Tabs[target]) = (mutable.Tabs[target], mutable.Tabs[editingTabIndex]);
        editingTabIndex = target;
        needsContentRebuild = true;
    }

    private void DeleteTab()
    {
        if (mutable.Tabs.Count <= 1)
            return;

        mutable.Tabs.RemoveAt(editingTabIndex);
        editingTabIndex = Math.Clamp(editingTabIndex, 0, mutable.Tabs.Count - 1);

        if (Plugin.LastTab >= mutable.Tabs.Count)
            Plugin.LastTab = mutable.Tabs.Count - 1;

        needsContentRebuild = true;
    }

    private void OpenRenamePrompt(int index)
    {
        if (renamePrompt is not null || index < 0 || index >= mutable.Tabs.Count)
            return;

        var tab = mutable.Tabs[index];

        var prompt = new NativeTextPrompt(Plugin, tab.Name, result =>
        {
            renamePrompt = null;

            if (result is null)
                return;

            tab.Name = result;
            needsContentRebuild = true;
        })
        {
            InternalName = "Chat2Ex_RenameSettingsTab",
            Title = Language.Options_Tabs_Name,
            Size = new Vector2(320.0f, 120.0f),
            DepthLayer = 13,
            DisableClose = true,
            DisableClamping = true,
        };

        prompt.Open();
        renamePrompt = prompt;
    }

    private NativeTextPrompt? renamePrompt;

    private void AddButtonRow(SettingsList list, params (string Label, Action OnClick)[] buttons)
    {
        if (buttons.Length == 0)
            return;

        var spacing = 6.0f;
        var width = (ContentWidth - spacing * (buttons.Length - 1)) / buttons.Length;

        var row = new HorizontalListNode
        {
            Size = new Vector2(ContentWidth, 26.0f),
            ItemSpacing = spacing,
        };

        row.Height = 26.0f;
        row.Width = ContentWidth;

        foreach (var (label, onClick) in buttons)
        {
            row.AddNode(new TextButtonNode
            {
                Size = new Vector2(width, 26.0f),
                String = label,
                OnClick = onClick,
            });
        }

        list.AddNode(row);
    }

    private void BuildChangelogTab(SettingsList list)
    {
        AddDescription(list, Language.Options_Warning_NotImplemented);

        AddOption(list,
            Language.Options_PrintChangelog_Name,
            Language.Options_PrintChangelog_Description,
            () => mutable.PrintChangelog,
            value => mutable.PrintChangelog = value);

        AddDescription(list, Language.Options_Changelog_Header);
        AddText(list, $"Version {Plugin.Interface.Manifest.AssemblyVersion.ToString(3)}");

        var changelog = Plugin.Interface.Manifest.Changelog;
        if (changelog is null)
            return;

        foreach (var line in changelog.Split('\n'))
            AddText(list, line.Length == 0 ? " " : line);
    }

    private void BuildAboutTab(SettingsList list)
    {
        AddText(list, string.Format(Language.Options_About_Opening, Plugin.PluginName));
        AddText(list, $"{Language.Options_About_Authors} {Plugin.Interface.Manifest.Author}");
        AddText(list, $"{Language.Options_About_Discord} @infi");
        AddText(list, $"{Language.Options_About_Version} {Plugin.Interface.Manifest.AssemblyVersion.ToString(3)}");

        AddLinkButton(list, Language.Options_About_Discord_Thread,
            "https://canary.discord.com/channels/581875019861328007/1224865018789761126");
        AddLinkButton(list, Language.Options_About_Github_Issues, "https://github.com/Infiziert90/ChatTwo/issues");
        AddLinkButton(list, Language.Options_About_CrowdIn, "https://crowdin.com/project/chattwo");

        AddDescription(list, Language.Options_About_Translators);
        AddText(list, string.Join(", ", Translators));
    }

    private static readonly string[] Translators =
    [
        "aislinn87", "Akizem", "AkiraYorumoto", "andrei5125", "andreycout", "Button_", "Cali666",
        "cassandra308", "d0tiKs", "Dark32", "elena.space", "imlisa", "jtabox", "lichuyuan",
        "lokinmodar", "MKhayle", "Moonlight_Everlit", "nishinatsu051", "q673135110", "Risu64",
        "ShivaMaheshvara", "Sirayuki", "witchymary", "yummymary", "Yuzumi", "zomsakura",
    ];

    private void AddText(SettingsList list, string text)
    {
        var width = ContentWidth;
        var node = new TextNode
        {
            TextFlags = TextFlags.WordWrap | TextFlags.MultiLine,
            FontSize = 12,
            LineSpacing = 15,
        };

        var lines = CountWrappedLines(text, width, 12);
        node.Size = new Vector2(width, lines * 16.0f);
        node.String = text;

        list.AddNode(node);
    }

    /// <summary>
    ///     Measures the unwrapped text width; <see cref="TextNode.GetTextDrawSize" /> cannot be used on
    ///     the target node because its word wrap flag makes it report a single wrapped line.
    /// </summary>
    private float MeasureWidth(string text, uint fontSize)
    {
        if (measureNode is null)
            return 0.0f;

        measureNode.FontSize = fontSize;
        measureNode.String = text;
        return measureNode.GetTextDrawSize(false).X;
    }

    /// <summary>
    ///     Greedy word wrap line count, used to size text nodes before they are added to the list.
    /// </summary>
    private int CountWrappedLines(string text, float width, uint fontSize)
    {
        if (string.IsNullOrEmpty(text) || width <= 1.0f)
            return 1;

        var lines = 0;

        foreach (var paragraph in text.Split('\n'))
        {
            lines++;

            var currentLine = 0.0f;

            foreach (var token in NativeTextWrap.Tokenize(paragraph))
            {
                if (token is "\n")
                    continue;

                var tokenWidth = MeasureWidth(token, fontSize);

                if (currentLine > 0.0f && currentLine + tokenWidth > width)
                {
                    lines++;
                    currentLine = tokenWidth;
                }
                else
                {
                    currentLine += tokenWidth;
                }
            }
        }

        return Math.Max(1, lines);
    }

    private void AddLinkButton(SettingsList list, string label, string url)
    {
        list.AddNode(new TextButtonNode
        {
            Size = new Vector2(ContentWidth, 26.0f),
            String = label,
            OnClick = () => Dalamud.Utility.Util.OpenLink(url),
        });
    }

    private void BuildMiscellaneousTab(SettingsList list)
    {
        AddDropDown(list,
            Language.Options_Language_Name,
            string.Format(Language.Options_Language_Description, Plugin.PluginName),
            mutable.LanguageOverride,
            Enum.GetValues<LanguageOverride>(),
            value => value.Name(),
            value => mutable.LanguageOverride = value);

        AddDropDown(list,
            Language.Options_CommandHelpSide_Name,
            string.Format(Language.Options_CommandHelpSide_Description, Plugin.PluginName),
            mutable.CommandHelpSide,
            Enum.GetValues<CommandHelpSide>(),
            value => value.Name(),
            value => mutable.CommandHelpSide = value);

        AddDropDown(list,
            Language.Options_KeybindMode_Name,
            string.Format(Language.Options_KeybindMode_Description, Plugin.PluginName),
            mutable.KeybindMode,
            Enum.GetValues<KeybindMode>(),
            value => value.Name(),
            value => mutable.KeybindMode = value);

        AddOption(list,
            Language.Options_SortAutoTranslate_Name,
            Language.Options_SortAutoTranslate_Description,
            () => mutable.SortAutoTranslate,
            value => mutable.SortAutoTranslate = value);
    }

    private void AddDropDown<T>(SettingsList list, string label, string? description, T current, IEnumerable<T> options, Func<T, string> labelFunc, Action<T> set)
    {
        list.AddNode(new TextNode
        {
            Size = new Vector2(ContentWidth, 16.0f),
            FontSize = 12,
            LineSpacing = 15,
            String = label,
        });

        var dropdown = new DropDownNode<T>
        {
            Size = new Vector2(ContentWidth, 24.0f),
            GetLabelFunction = entry => labelFunc(entry),
        };

        dropdown.Options = options.ToList();
        dropdown.SelectedOption = current;
        dropdown.OnOptionSelected = value =>
        {
            set(value);
            needsContentRebuild = true;
        };

        list.AddNode(dropdown);

        if (!string.IsNullOrEmpty(description))
            AddDescription(list, description);
    }

    private void AddOption(SettingsList list, string label, string? description, Func<bool> get, Action<bool> set)
    {
        var checkbox = new CheckboxNode
        {
            String = label,
            IsChecked = get(),
            OnClick = value =>
            {
                set(value);
                needsContentRebuild = true;
            },
        };

        // CheckboxNode sizes its label from its own height, so the height has to be set explicitly or
        // the node reports zero height and the list stacks all rows on top of each other.
        checkbox.Height = 22.0f;
        checkbox.Width = ContentWidth;

        list.AddNode(checkbox);

        if (!string.IsNullOrEmpty(description))
            AddDescription(list, description);
    }

    private void AddIntOption(SettingsList list, string label, string? description, Func<int> get, Action<int> set, int min, int max)
    {
        list.AddNode(new TextNode
        {
            Size = new Vector2(ContentWidth, 16.0f),
            FontSize = 12,
            LineSpacing = 15,
            String = label,
        });

        // The stepper has buttons on both sides, the remaining space holds the digits. It has to fit
        // the largest value, otherwise the digits are cut off.
        var digits = max.ToString(CultureInfo.InvariantCulture).Length;
        var inputWidth = Math.Max(80.0f, 62.0f + digits * 8.0f);

        var input = new NumericInputNode
        {
            Size = new Vector2(inputWidth, 24.0f),
            Min = min,
            Max = max,
            Value = Math.Clamp(get(), min, max),
            OnValueUpdate = value =>
            {
                set(value);
                needsContentRebuild = true;
            },
        };

        list.AddNode(input);

        if (!string.IsNullOrEmpty(description))
            AddDescription(list, description);
    }

    private void AddDescription(SettingsList list, string description)
    {
        var width = ContentWidth;

        var text = new TextNode
        {
            TextFlags = TextFlags.WordWrap | TextFlags.MultiLine,
            FontSize = 11,
            LineSpacing = 14,
            TextColor = DescriptionColor,
        };

        var lines = CountWrappedLines(description, width, 11);
        text.Size = new Vector2(width, lines * 15.0f + 2.0f);
        text.String = description;

        list.AddNode(text);
    }

    private void Save(bool close)
    {
        var hideChanged = !mutable.HideChat && mutable.HideChat != Plugin.Config.HideChat;

        Plugin.Config.UpdateFrom(mutable, true);
        Plugin.DeferredSaveFrames = 60;

        Plugin.MessageManager.ClearAllTabs();
        Plugin.MessageManager.FilterAllTabsAsync();

        // The filtered message lists changed without necessarily changing their length, so the native
        // windows have to drop their cached state and rebuild.
        Plugin.ChatUi.InvalidateAll();

        if (hideChanged)
            GameFunctions.GameFunctions.SetChatInteractable(true);

        mutable = new Configuration();
        mutable.UpdateFrom(Plugin.Config, false);

        needsContentRebuild = true;

        if (close)
            CloseSettings();
    }

    private void CloseSettings()
    {
        // Never dispose the addon from inside one of its own button callbacks: the game is still
        // processing the click and would dereference the freed node tree. Defer to the next tick.
        Plugin.Framework.RunOnTick(() =>
        {
            // Dispose so KamiToolKit stops tracking this addon, otherwise it gets flagged on unload.
            Dispose();
            onRequestClose();
        });
    }

    public override void Dispose()
    {
        colorPicker?.Dispose();
        colorPicker = null;

        renamePrompt?.Dispose();
        renamePrompt = null;

        measureNode?.Dispose();
        measureNode = null;

        base.Dispose();
    }
}
