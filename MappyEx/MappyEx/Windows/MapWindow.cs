using System;
using System.Drawing;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using KamiLib.Classes;
using KamiLib.CommandManager;
using KamiLib.Window;
using Lumina.Excel.Sheets;
using Mappy.Classes;
using Mappy.Classes.MapWindowComponents;
using Mappy.Controllers;
using Mappy.Data;
using Map = Lumina.Excel.Sheets.Map;

namespace Mappy.Windows;

public class MapWindow : Window
{
    public Vector2 MapDrawOffset { get; private set; }
    public HoverFlags HoveredFlags { get; private set; }
    public bool ProcessingCommand { get; set; }

    /// <summary>
    /// Set while drawing if a marker opened its own context menu,
    /// the map context menu has to stay out of the way in that case.
    /// </summary>
    public bool MarkerMenuOpened { get; set; }

    private bool isDragStarted;
    private Vector2 lastWindowSize;
    private uint lastMapId;
    private uint lastAreaPlaceNameId;
    private uint lastSubAreaPlaceNameId;

    private readonly MapToolbar mapToolbar = new();
    private readonly MapBottomBar mapBottomBar = new();
    private readonly MapContextMenu mapContextMenu = new();
    private readonly MapSidebar mapSidebar = new();
    private readonly MapSearchOverlay mapSearch = new();

    /// <summary>
    /// Sidebar width that is currently added to the window, used to grow and shrink it when the browser opens.
    /// </summary>
    private float appliedSidebarWidth;
    private bool sidebarResizeInitialized;

    public MapWindow() : base("###MappyMapWindow", new Vector2(400.0f, 250.0f))
    {
        UpdateTitle();

        DisableWindowSounds = true;
        RegisterCommands();
    }

    public override bool DrawConditions() => IntegrationsController.ShouldShowMap();

    public override unsafe void PreOpenCheck()
    {
        // If you managed to open the window while the agent says it should be closed
        if (System.MapWindow.IsOpen && AgentMap.Instance()->AddonId is 0)
        {
            Service.Log.Debug("[OnShow] MapWindow can not be open now.");
            IsOpen = false;
        }
        
        if (System.SystemConfig.KeepOpen) {
            IsOpen = true;
        }

        if (Service.ClientState is { IsLoggedIn: false } or { IsPvP: true }) IsOpen = false;
    }

    public override unsafe void OnOpen()
    {
        if (ProcessingCommand) {
            ProcessingCommand = false;
            System.SystemConfig.FollowPlayer = false;
            return;
        }

        if (System.SystemConfig.FollowOnOpen) {
            System.IntegrationsController.OpenOccupiedMap();
            System.SystemConfig.FollowPlayer = true;
        }

        switch (System.SystemConfig.CenterOnOpen) {
            case CenterTarget.Player when Service.ObjectTable.LocalPlayer is { } localPlayer:
                System.MapRenderer.CenterOnGameObject(localPlayer);
                break;

            case CenterTarget.Map:
                System.SystemConfig.FollowPlayer = false;
                System.MapRenderer.CenterOnMapContent(AgentMap.Instance()->SelectedMapId);
                break;

            case CenterTarget.Disabled:
            default:
                break;
        }
    }

    protected override void DrawContents()
    {
        UpdateTitle();
        UpdateStyle();
        UpdateSizePosition();

        System.MapRenderer.UpdateMapCentering();

        // Reset every frame, set by any marker that was right clicked during drawing.
        MarkerMenuOpened = false;

        HoveredFlags = HoverFlags.Nothing;

        if (WindowBounds.IsBoundedBy(ImGui.GetMousePos(), ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + ImGui.GetContentRegionMax())) {
            HoveredFlags |= HoverFlags.Window;
        }

        UpdateSidebarResize();

        var contentRegion = ImGui.GetContentRegionAvail();
        var sidebarWidth = System.SystemConfig.ShowMapSidebar ? System.SystemConfig.MapSidebarWidth * ImGuiHelpers.GlobalScale : 0.0f;
        var mapWidth = contentRegion.X - (sidebarWidth is 0.0f ? 0.0f : sidebarWidth + ImGui.GetStyle().ItemSpacing.X);
        var mapSize = new Vector2(MathF.Max(mapWidth, 120.0f), contentRegion.Y);

        MapDrawOffset = ImGui.GetCursorScreenPos();
        using var fade = ImRaii.PushStyle(ImGuiStyleVar.Alpha, System.SystemConfig.FadePercent, ShouldFade());
        using (var renderChild = ImRaii.Child("render_child", mapSize, false, ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar)) {
            if (!renderChild) return;

            if (!System.SystemConfig.AcceptedSpoilerWarning) {
                DrawSpoilerWarning();
            }
            else {
                DrawMapElements();

                // Reset Draw Position for Overlay Extras
                ImGui.SetCursorPos(Vector2.Zero);
                DrawToolbar();
                DrawBottomBar();
                DrawMapSearch();
            }
        }

        if (ImGui.IsItemHovered()) {
            HoveredFlags |= HoverFlags.WindowInnerFrame;
        }

        // Read the map clicks before the sidebar becomes the last item of this window.
        var mapLeftClicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var mapRightClicked = ImGui.IsItemClicked(ImGuiMouseButton.Right);

        if (sidebarWidth is not 0.0f) {
            ImGui.SameLine();
            DrawSidebar(sidebarWidth, contentRegion.Y);
        }

        // Process Inputs
        ProcessInputs(mapLeftClicked, mapRightClicked);
    }

    /// <summary>
    /// Opening the browser grows the window by the width of the sidebar, so the visible part of the map keeps
    /// its size instead of being squeezed. Closing it shrinks the window again.
    /// </summary>
    private void UpdateSidebarResize()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var targetWidth = System.SystemConfig.ShowMapSidebar ? System.SystemConfig.MapSidebarWidth * ImGuiHelpers.GlobalScale + spacing : 0.0f;

        if (!sidebarResizeInitialized) {
            sidebarResizeInitialized = true;
            appliedSidebarWidth = targetWidth;

            return;
        }

        var change = targetWidth - appliedSidebarWidth;
        appliedSidebarWidth = targetWidth;

        if (MathF.Abs(change) < 0.5f) return;

        var size = ImGui.GetWindowSize();
        var minimumWidth = SizeConstraints?.MinimumSize.X ?? 200.0f * ImGuiHelpers.GlobalScale;
        var width = MathF.Max(size.X + change, minimumWidth);

        // Keep the window on screen when it is dragged all the way to the right edge.
        var position = ImGui.GetWindowPos();
        var displaySize = ImGui.GetIO().DisplaySize;
        if (position.X + width > displaySize.X) position.X = MathF.Max(0.0f, displaySize.X - width);

        ImGui.SetWindowSize(new Vector2(width, size.Y));
        ImGui.SetWindowPos(position);

        System.SystemConfig.WindowSize = new Vector2(width, size.Y);
        System.SystemConfig.WindowPosition = position;
    }

    private void DrawMapSearch()
    {
        if (!mapSearch.IsOpen) return;

        mapSearch.Draw();

        if (ImGui.IsItemHovered()) {
            HoveredFlags |= HoverFlags.Overlay;
        }
    }

    private void DrawSidebar(float width, float height)
    {
        using var sidebar = ImRaii.Child("map_sidebar", new Vector2(width, height), true);
        if (!sidebar) return;

        mapSidebar.Draw();

        if (ImGui.IsItemHovered()) {
            HoveredFlags |= HoverFlags.Sidebar;
        }
    }

    public void ToggleMapSearch() => mapSearch.Toggle();

    public bool IsMapSearchOpen => mapSearch.IsOpen;

    public void ToggleSidebar()
    {
        System.SystemConfig.ShowMapSidebar = !System.SystemConfig.ShowMapSidebar;
        SystemConfig.Save();
    }

    private void DrawMapElements()
    {
        System.MapRenderer.DrawBaseTexture();
        if (ImGui.IsItemHovered()) {
            HoveredFlags |= HoverFlags.MapTexture;
        }

        System.MapRenderer.DrawDynamicElements();
    }

    private void DrawToolbar()
    {
        if (!ShouldShowToolbar()) return;

        using (ImRaii.Group()) {
            mapToolbar.Draw();
        }

        if (ImGui.IsItemHovered()) {
            HoveredFlags |= HoverFlags.Toolbar;
        }
    }

    private void DrawBottomBar()
    {
        // Shares the visibility with the top toolbar.
        if (!ShouldShowToolbar()) return;

        var isHovered = false;

        using (ImRaii.Group()) {
            isHovered = mapBottomBar.Draw(HoveredFlags.HasFlag(HoverFlags.MapTexture), MapDrawOffset);
        }

        if (isHovered) {
            HoveredFlags |= HoverFlags.CoordinateBar;
        }
    }

    private bool ShouldShowToolbar()
    {
        if (isDragStarted) return false;
        if (System.SystemConfig.ShowToolbarOnHover && HoveredFlags.Any()) return true;
        if (System.SystemConfig.AlwaysShowToolbar) return true;

        return false;
    }

    private unsafe void UpdateTitle()
    {
        var mapChanged = lastMapId != AgentMap.Instance()->SelectedMapId;
        var areaChanged = lastAreaPlaceNameId != TerritoryInfo.Instance()->AreaPlaceNameId;
        var subAreaChanged = lastSubAreaPlaceNameId != TerritoryInfo.Instance()->SubAreaPlaceNameId;
        var locationChanged = mapChanged || areaChanged || subAreaChanged;

        if (!locationChanged) return;
        var subLocationString = string.Empty;
        var mapData = Service.DataManager.GetExcelSheet<Map>().GetRow(AgentMap.Instance()->SelectedMapId);

        if (System.SystemConfig.ShowRegionLabel) {
            var mapRegionName = mapData.PlaceNameRegion.Value.Name.ExtractText();
            subLocationString += $" - {mapRegionName}";
        }

        if (System.SystemConfig.ShowMapLabel) {
            var mapName = mapData.PlaceName.Value.Name.ExtractText();
            subLocationString += $" - {mapName}";
        }

        // Don't show specific locations if we aren't there.
        if (AgentMap.Instance()->SelectedMapId == AgentMap.Instance()->CurrentMapId) {
            if (TerritoryInfo.Instance()->AreaPlaceNameId is not 0 && System.SystemConfig.ShowAreaLabel) {
                var areaLabel = Service.DataManager.GetExcelSheet<PlaceName>().GetRow(TerritoryInfo.Instance()->AreaPlaceNameId);
                subLocationString += $" - {areaLabel.Name}";
            }

            if (TerritoryInfo.Instance()->SubAreaPlaceNameId is not 0 && System.SystemConfig.ShowSubAreaLabel) {
                var subAreaLabel = Service.DataManager.GetExcelSheet<PlaceName>().GetRow(TerritoryInfo.Instance()->SubAreaPlaceNameId);
                subLocationString += $" - {subAreaLabel.Name}";
            }
        }

        WindowName = $"{subLocationString}###MappyMapWindow".TrimStart(['-',' ']);

        lastMapId = AgentMap.Instance()->SelectedMapId;
        lastAreaPlaceNameId = TerritoryInfo.Instance()->AreaPlaceNameId;
        lastSubAreaPlaceNameId = TerritoryInfo.Instance()->SubAreaPlaceNameId;
    }

    public void RefreshTitle()
    {
        lastMapId = 0;
        lastAreaPlaceNameId = 0;
        lastSubAreaPlaceNameId = 0;
    }

    private void ProcessInputs(bool mapLeftClicked, bool mapRightClicked)
    {
        // A marker opened its own context menu this frame, the map menu has to stay closed.
        if (MarkerMenuOpened) {
            ProcessMapDragDragging();
            ProcessMapDragEnd();
            return;
        }

        // Clicks inside the search box, the browser or the bottom bar belong to them, not to the map.
        var isOverUi = HoveredFlags.HasFlag(HoverFlags.Overlay) || HoveredFlags.HasFlag(HoverFlags.Sidebar) ||
                       HoveredFlags.HasFlag(HoverFlags.CoordinateBar);

        if (mapRightClicked && !isOverUi) {
            ImGui.OpenPopup("Mappy_Context_Menu");
        }
        else {
            if (HoveredFlags.Any()) {
                if (System.SystemConfig.EnableShiftDragMove && ImGui.GetIO().KeyShift) {
                    Flags &= ~ImGuiWindowFlags.NoMove;
                }
                else {
                    if (!isOverUi) {
                        ProcessMouseScroll();
                        ProcessMapDragStart(mapLeftClicked);
                    }

                    Flags |= ImGuiWindowFlags.NoMove;
                }
            }

            ProcessMapDragDragging();
            ProcessMapDragEnd();
        }

        // Draw Context Menu
        mapContextMenu.Draw(MapDrawOffset);
    }

    private unsafe void UpdateStyle()
    {
        if (System.SystemConfig.HideWindowFrame) {
            Flags |= ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground;
        }
        else {
            Flags &= ~(ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground);
        }

        if (System.SystemConfig.LockWindow) {
            Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
        }
        else {
            Flags &= ~(ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);
        }

        if (System.SystemConfig.NoFocusOnAppear) {
            Flags |= ImGuiWindowFlags.NoFocusOnAppearing;
        }
        else {
            Flags &= ~ImGuiWindowFlags.NoFocusOnAppearing;
        }

        if (System.SystemConfig.HideWindowBackground) {
            Flags |= ImGuiWindowFlags.NoBackground;
        }
        else {
            Flags &= ~ImGuiWindowFlags.NoBackground;
        }

        // While the search box is open, escape closes it instead of the map.
        if (Service.KeyState[VirtualKey.ESCAPE] && IsFocused && !IsMapLocked() && !mapSearch.IsOpen) {
            AgentMap.Instance()->Hide();
        }

        if (System.SystemConfig.FollowPlayer && Service.ObjectTable is { LocalPlayer: { } localPlayer }) {
            System.MapRenderer.CenterOnGameObject(localPlayer);
        }

        if (System.SystemConfig.LockCenterOnMap) {
            System.SystemConfig.FollowPlayer = false;
            System.MapRenderer.DrawOffset = Vector2.Zero;
        }
    }

    private void UpdateSizePosition()
    {
        var systemConfig = System.SystemConfig;
        var windowPosition = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();

        if (!IsFocused) {
            if (windowPosition != systemConfig.WindowPosition) {
                ImGui.SetWindowPos(systemConfig.WindowPosition);
            }

            if (windowSize != systemConfig.WindowSize) {
                ImGui.SetWindowSize(systemConfig.WindowSize);
            }
        }
        else {
            // If focused
            if (systemConfig.WindowPosition != windowPosition) {
                systemConfig.WindowPosition = windowPosition;
                SystemConfig.Save();
            }

            if (systemConfig.WindowSize != windowSize) {
                systemConfig.WindowSize = windowSize;
                SystemConfig.Save();
            }
        }
    }

    private static void DrawSpoilerWarning()
    {
        using (ImRaii.PushColor(ImGuiCol.Text, KnownColor.Orange.Vector())) {
            var warningLine1 = Localization.Get("Map.SpoilerWarning1", "Warning, Mappy does not protect you from spoilers and will show everything.");
            var warningLine2 = Localization.Get("Map.SpoilerWarning2", "Do not use Mappy if you are not comfortable with this.");

            ImGui.SetCursorPos(ImGui.GetContentRegionAvail() / 2.0f - (ImGui.CalcTextSize(warningLine1) * 2.0f) with { X = 0.0f });
            ImGuiHelpers.CenteredText(warningLine1);
            ImGuiHelpers.CenteredText(warningLine2);
        }

        ImGuiHelpers.ScaledDummy(30.0f);
        ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X / 3.0f);
        using (ImRaii.Disabled(!(ImGui.GetIO().KeyShift && ImGui.GetIO().KeyCtrl))) {
            if (ImGui.Button(Localization.Get("Map.IUnderstand", "I understand"), new Vector2(ImGui.GetContentRegionAvail().X / 2.0f, 23.0f * ImGuiHelpers.GlobalScale))) {
                System.SystemConfig.AcceptedSpoilerWarning = true;
                SystemConfig.Save();
            }

            using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, 1.0f)) {
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
                    ImGui.SetTooltip(Localization.Get("Map.IUnderstandTooltip", "Hold Shift + Control while clicking activate button"));
                }
            }
        }
    }

    public override unsafe void OnClose()
    {
        mapSearch.Close();

        AgentMap.Instance()->Hide();

        SystemConfig.Save();
    }

    private void ProcessMouseScroll()
    {
        if (System.SystemConfig.ZoomLocked) return;

        var wheel = ImGui.GetIO().MouseWheel;
        if (wheel is 0.0f) return;
        if (!HoveredFlags.HasFlag(HoverFlags.WindowInnerFrame)) return;

        // Some mice report several wheel clicks inside one frame (and some trackpads report fractions),
        // clamp so one physical notch always translates into exactly one zoom step.
        var step = Math.Clamp(wheel, -1.0f, 1.0f);

        // Always zoom by a fixed ratio, a fixed absolute step feels lightning fast when the map is small
        // and does nothing once it is zoomed in far enough.
        var factor = MathF.Pow(1.0f + System.SystemConfig.ZoomSpeed, step);

        MapRenderer.MapRenderer.Scale = Math.Clamp(MapRenderer.MapRenderer.Scale * factor, 0.05f, 20.0f);
    }

    private void ProcessMapDragDragging()
    {
        if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && isDragStarted) {
            System.MapRenderer.DrawOffset += ImGui.GetMouseDragDelta() / MapRenderer.MapRenderer.Scale;
            ImGui.ResetMouseDragDelta();
        }
    }

    private void ProcessMapDragEnd()
    {
        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
            isDragStarted = false;
        }
    }

    private void ProcessMapDragStart(bool mapClicked)
    {
        // Don't allow a drag to start if the window size is changing
        if (ImGui.GetWindowSize() == lastWindowSize) {
            if (mapClicked && !isDragStarted) {
                isDragStarted = true;
                System.SystemConfig.FollowPlayer = false;
            }
        }
        else {
            lastWindowSize = ImGui.GetWindowSize();
            isDragStarted = false;
        }
    }

    private unsafe bool ShouldFade() =>
        System.SystemConfig.FadeMode.HasFlag(FadeMode.Always) ||
        System.SystemConfig.FadeMode.HasFlag(FadeMode.WhenFocused) && IsFocused ||
        System.SystemConfig.FadeMode.HasFlag(FadeMode.WhenMoving) && AgentMap.Instance()->IsPlayerMoving ||
        System.SystemConfig.FadeMode.HasFlag(FadeMode.WhenUnFocused) && !IsFocused;

    private unsafe void RegisterCommands()
    {
        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            UseShowHideText = true,
            BaseActivationPath = "/map",
            EnableDelegate = _ => System.MapWindow.UnCollapseOrShow(),
            DisableDelegate = _ => System.MapWindow.Close(),
            ToggleDelegate = _ => System.MapWindow.UnCollapseOrToggle(),
        });

        System.CommandManager.RegisterCommand(new CommandHandler
        {
            ActivationPath = "/map/follow",
            Delegate = _ =>
            {
                System.SystemConfig.FollowPlayer = true;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new CommandHandler
        {
            ActivationPath = "/map/unfollow",
            Delegate = _ =>
            {
                System.SystemConfig.FollowPlayer = false;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            BaseActivationPath = "/autofollow",
            EnableDelegate = _ =>
            {
                System.SystemConfig.FollowOnOpen = true;
                SystemConfig.Save();
            },
            DisableDelegate = _ =>
            {
                System.SystemConfig.FollowOnOpen = false;
                SystemConfig.Save();
            },
            ToggleDelegate = _ =>
            {
                System.SystemConfig.FollowOnOpen = !System.SystemConfig.FollowOnOpen;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new ToggleCommandHandler
        {
            BaseActivationPath = "/keepopen",
            EnableDelegate = _ =>
            {
                System.SystemConfig.KeepOpen = true;
                SystemConfig.Save();
            },
            DisableDelegate = _ =>
            {
                System.SystemConfig.KeepOpen = false;
                SystemConfig.Save();
            },
            ToggleDelegate = _ =>
            {
                System.SystemConfig.KeepOpen = !System.SystemConfig.KeepOpen;
                SystemConfig.Save();
            },
        });

        System.CommandManager.RegisterCommand(new CommandHandler
        {
            ActivationPath = "/center/player",
            Delegate = _ =>
            {
                if (Service.ObjectTable.LocalPlayer is { } localPlayer) {
                    System.MapRenderer.CenterOnGameObject(localPlayer);
                }
            },
        });

        System.CommandManager.RegisterCommand(new CommandHandler
        {
            ActivationPath = "/center/map",
            Delegate = _ =>
            {
                System.SystemConfig.FollowPlayer = false;
                System.MapRenderer.CenterOnMapContent(AgentMap.Instance()->SelectedMapId);
            },
        });
    }

    private static unsafe bool IsMapLocked()
    {
        var addon = Service.GameGui.GetAddonByName<AddonAreaMap>("AreaMap");
        if (addon is null || addon->RootNode is null) return false;

        return (addon->Param & 0x8_0000) > 0;
    }
}