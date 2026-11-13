using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Chat2Ex.Http;
using Chat2Ex.Ipc;
using Chat2Ex.Native;
using Chat2Ex.Resources;
using Chat2Ex.Ui;
using Chat2Ex.Util;
using KamiToolKit;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using FFXIVClientStructs.FFXIV.Client.Enums;

namespace Chat2Ex;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class Plugin : IDalamudPlugin
{
    public const string PluginName = "Chat2Ex";

    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IDalamudPluginInterface Interface { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IKeyState KeyState { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static IPartyList PartyList { get; private set; } = null!;
    [PluginService] public static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] public static INotificationManager Notification { get; private set; } = null!;
    [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] public static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] public static ISeStringEvaluator Evaluator { get; private set; } = null!;

    public static Configuration Config = null!;


    /// <summary>
    ///     Chat state that used to live in the ImGui chat log (hide state, current input, activation).
    /// </summary>
    public ChatState ChatLog { get; }

    /// <summary>
    ///     Native tool windows (DB viewer, debugger, SeString debugger).
    /// </summary>
    public NativeToolWindows ToolWindows { get; }

    /// <summary>
    ///     Native (KamiToolKit) UI layer that replaces the ImGui chat log.
    /// </summary>
    public NativeChatUi ChatUi { get; }

    /// <summary>
    ///     Native (KamiToolKit) settings window.
    /// </summary>
    public NativeSettingsUi NativeSettings { get; }

    /// <summary>
    ///     Set once KamiToolKit finished initializing; native windows may only be created after that.
    /// </summary>
    public bool KamiToolKitReady { get; private set; }

    public Commands Commands { get; }
    public GameFunctions.GameFunctions Functions { get; }
    public MessageManager MessageManager { get; }
    public IpcManager Ipc { get; }
    public ExtraChat ExtraChat { get; }
    public TypingIpc TypingIpc { get; }
    public FontManager FontManager { get; }

    public readonly ServerCore ServerCore;

    public int DeferredSaveFrames = -1;

    public DateTime GameStarted { get; }

    // Tab management needs to happen outside the chatlog window class for access reasons
    public int LastTab { get; set; }
    public int? WantedTab { get; set; }
    public Tab CurrentTab
    {
        get
        {
            var i = LastTab;
            return i > -1 && i < Config.Tabs.Count ? Config.Tabs[i] : new Tab();
        }
    }

    public Plugin()
    {
        try
        {
            GameStarted = Process.GetCurrentProcess().StartTime.ToUniversalTime();

            Config = Interface.GetPluginConfig() as Configuration ?? new Configuration();

            if (Config.Tabs.Count == 0)
                Config.Tabs.Add(TabsUtil.VanillaGeneral);

            LanguageChanged(Interface.UiLanguage);

            // This is called by followup functions if the player is already logged in
            ServerCore = new ServerCore(this);

            Commands = new Commands();
            Functions = new GameFunctions.GameFunctions(this);
            Ipc = new IpcManager();
            TypingIpc = new TypingIpc(this);
            ExtraChat = new ExtraChat();
            FontManager = new FontManager();

            MessageManager = new MessageManager(this); // Does it require UI?

            ChatLog = new ChatState(this);
            ChatUi = new NativeChatUi(this);
            NativeSettings = new NativeSettingsUi(this);
            ToolWindows = new NativeToolWindows(this);

            // All windows are native addons (KamiToolKit); nothing is rendered through ImGui anymore.

            Interface.UiBuilder.DisableCutsceneUiHide = true;
            Interface.UiBuilder.DisableGposeUiHide = true;

            // let all the other components register, then initialize commands
            Commands.Initialise();

            if (Interface.Reason is not PluginLoadReason.Boot)
                MessageManager.FilterAllTabsAsync();

            Framework.Update += FrameworkUpdate;
            Interface.UiBuilder.Draw += Draw;
            Interface.UiBuilder.OpenMainUi += OpenMainUi;
            Interface.LanguageChanged += LanguageChanged;

            if (Config.ShowEmotes)
                Task.Run(EmoteCache.LoadData);

            #if !DEBUG
            // Avoid 300ms hitch when sending first message by preloading the
            // auto-translate cache. Don't do this in debug because it makes
            // profiling difficult.
            AutoTranslate.PreloadCache();
            #endif

            // Automatically start the webserver if requested
            if (Config is { WebinterfaceEnabled: true, WebinterfaceAutoStart: true })
            {
                Task.Run(() =>
                {
                    ServerCore.Start();
                    ServerCore.Run();
                });
            }

            // KamiToolKit must be initialized before any native node is created. This cannot be awaited
            // from the plugin constructor (it hops to the framework thread), so the native UI waits for
            // KamiToolKitReady instead. Keep the task so Dispose can tell whether initialization ever
            // started and always tear down the hooks it installed.
            kamiToolKitInitStarted = true;
            kamiToolKitInitTask = InitializeKamiToolKitAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Plugin load threw an error, turning off plugin");
            Dispose();

            // Re-throw the exception to fail the plugin load.
            throw;
        }
    }

    // Suppressing this warning because Dispose() is called in Plugin() if the
    // load fails, so some values may not be initialized.
    [SuppressMessage("ReSharper", "ConditionalAccessQualifierIsNonNullableAccordingToAPIContract")]
    public void Dispose()
    {
        disposing = true;
        NativeCallback.ClearDeferred();

        if (kamiToolKitInitTask is { IsCompleted: false })
            Log.Warning("KamiToolKit initialization was still in flight during disposal");

        Interface.LanguageChanged -= LanguageChanged;
        Interface.UiBuilder.Draw -= Draw;
        Interface.UiBuilder.OpenMainUi -= OpenMainUi;
        Framework.Update -= FrameworkUpdate;
        NativeCallback.Safe(() => GameFunctions.GameFunctions.SetChatInteractable(true), "restoring chat interactability");

        ChatUi?.Dispose();
        NativeSettings?.Dispose();
        ToolWindows?.Dispose();
        ChatLog?.Dispose();

        TypingIpc?.Dispose();
        ExtraChat?.Dispose();
        Ipc?.Dispose();
        MessageManager?.DisposeAsync().AsTask().Wait();
        Functions?.Dispose();
        Commands?.Dispose();

        EmoteCache.Dispose();
        ServerCore?.DisposeAsync().AsTask().Wait();

        // Always tear down KTK, even when initialization was still in flight or failed.
        // InitializeAsync installs a process-wide FireCallback hook before it completes; leaving
        // that hook (and its delegate into this plugin's assembly load context) behind crashes the
        // game on the next plugin reload. Gating this on KamiToolKitReady was the original bug.
        if (kamiToolKitInitStarted)
        {
            void DisposeKtk() => NativeCallback.Safe(KamiToolKitLibrary.Dispose, "disposing KamiToolKit");

            if (Dalamud.Utility.ThreadSafety.IsMainThread)
                DisposeKtk();
            else
                Framework.RunOnFrameworkThread(DisposeKtk);
        }
    }

    /// <summary>
    ///     Entry point for Dalamud's main UI, opens the chat window and focuses the chat input.
    /// </summary>
    private void OpenMainUi() => NativeCallback.Safe(() => ChatUi?.OpenMainWindow(), "opening the main UI");

    /// <summary>
    ///     Runs on every UiBuilder.Draw. This is a game callback: an exception here is not caught by
    ///     Dalamud and terminates the process, so the whole body stays behind NativeCallback.Safe.
    /// </summary>
    private void Draw() => NativeCallback.Safe(drawCore ??= DrawCore, "drawing the chat UI");

    private Action? drawCore;

    private void DrawCore()
    {
        // Drain deferred UI actions (addon creation requested from context menu callbacks) once the
        // game has finished unwinding the menu's FireCallback / hide chain.
        NativeCallback.TickDeferred();

        ChatLog.BeginFrame();

        if (Config.HideInLoadingScreens && Condition[ConditionFlag.BetweenAreas])
        {
            ChatUi.SetHidden(true);
            ChatLog.FinalizeFrame();
            TypingIpc.Update();
            return;
        }

        ChatLog.IsHidden = HideStateHelper.HideStateCheck(ChatLog, Config.HideInBattle, Config.HideDuringCutscenes, Config.HideWhenNotLoggedIn, ChatLog.Activate);

        ChatLog.FinalizeFrame();

        // The native window has to be updated after FinalizeFrame, since it owns the input focus state.
        ChatUi.Update();
        NativeSettings.Update();

        TypingIpc.Update();
    }

    private bool disposing;
    private bool kamiToolKitInitStarted;
    private Task? kamiToolKitInitTask;

    private async Task InitializeKamiToolKitAsync()
    {
        try
        {
            await KamiToolKitLibrary.InitializeAsync(Interface);

            if (!disposing)
                KamiToolKitReady = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "KamiToolKit initialization failed, native windows will not be available");
        }
    }

    public void SaveConfig()
    {
        Interface.SavePluginConfig(Config);
    }

    public void LanguageChanged(string langCode) => NativeCallback.Safe(() =>
    {
        var info = Config.LanguageOverride is LanguageOverride.None
            ? new CultureInfo(langCode)
            : new CultureInfo(Config.LanguageOverride.Code());

        Language.Culture = info;
    }, "changing the language");

    private static readonly string[] ChatAddonNames =
    [
        "ChatLog",
        "ChatLogPanel_0",
        "ChatLogPanel_1",
        "ChatLogPanel_2",
        "ChatLogPanel_3",
    ];

    /// <summary>
    ///     Framework.Update is also a game callback. Keep the delegate cached so the guard itself
    ///     does not allocate on every tick.
    /// </summary>
    private void FrameworkUpdate(IFramework framework)
        => NativeCallback.Safe(frameworkUpdateCore ??= FrameworkUpdateCore, "updating framework state");

    private Action? frameworkUpdateCore;

    private void FrameworkUpdateCore()
    {
        if (DeferredSaveFrames >= 0 && DeferredSaveFrames-- == 0)
            SaveConfig();

        if (!Config.HideChat)
            return;

        foreach (var name in ChatAddonNames)
            if (GameFunctions.GameFunctions.IsAddonInteractable(name))
                GameFunctions.GameFunctions.SetAddonInteractable(name, false);
    }

    public static bool InBattle => Condition[ConditionFlag.InCombat];
    public static bool GposeActive => Condition[ConditionFlag.WatchingCutscene];
    public static bool CutsceneActive => Condition[ConditionFlag.OccupiedInCutSceneEvent] || Condition[ConditionFlag.WatchingCutscene78];

    // M1: this used to run a Lumina sheet lookup on every frame from both Draw and the keybind handler.
    private static uint cachedBrawlerTerritory = uint.MaxValue;
    private static bool cachedBrawlerResult;

    public static bool InKeyboundBrawler
    {
        get
        {
            var territory = ClientState.TerritoryType;
            if (territory == cachedBrawlerTerritory)
                return cachedBrawlerResult;

            cachedBrawlerTerritory = territory;
            cachedBrawlerResult = Sheets.TerritorySheet.TryGetRow(territory, out var territoryType)
                && territoryType.TerritoryIntendedUse.RowId == (uint) TerritoryIntendedUse.KeyboundBrawler;
            return cachedBrawlerResult;
        }
    }
}
