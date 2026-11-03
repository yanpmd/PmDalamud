using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using KamiLib.Classes;
using KamiLib.CommandManager;
using KamiLib.Extensions;
using KamiLib.Window;
using Mappy.Classes;
using Mappy.Data;

namespace Mappy.Windows;

public class ConfigurationWindow : Window
{
    // Ordered from the settings that are touched most often to the ones that are set once and forgotten.
    private readonly TabBar tabBar = new("mappy_tab_bar", [
        new MapFunctionsTab(),
        new StyleOptionsTab(),
        new NavigationTab(),
        new MarkerGroupsTab(),
        new IconConfigurationTab(),
    ]);

    public ConfigurationWindow() : base(Localization.Get("Window.Configuration", "Mappy Configuration Window"), new Vector2(500.0f, 580.0f))
    {
        System.CommandManager.RegisterCommand(new CommandHandler
        {
            Delegate = _ => System.ConfigWindow.Toggle(), ActivationPath = "/",
        });
    }

    public void UpdateLocalizedName()
        => WindowName = $"{Localization.Get("Window.Configuration", "Mappy Configuration Window")}##{Service.PluginInterface.InternalName}";

    protected override void DrawContents()
    {
        tabBar.Draw();

        // Language applies to the whole plugin, keeping it out of a page frees up the first screen of that page.
        ImGui.Separator();
        DrawLanguageSelector();
    }

    private static void DrawLanguageSelector()
    {
        var languages = Localization.SupportedLanguages;
        var currentIndex = Array.FindIndex(languages, entry => entry.Code == System.SystemConfig.Language);
        if (currentIndex < 0) currentIndex = 0;

        var languageNames = languages.Select(entry => Localization.Get(entry.NameKey)).ToArray();

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(Localization.Get("Language.Header", "Language"));
        ImGui.SameLine();

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.5f);
        if (!ImGui.Combo("##mappy_language", ref currentIndex, languageNames, languageNames.Length)) return;

        System.SystemConfig.Language = languages[currentIndex].Code;
        Localization.Load(System.SystemConfig.GetLanguageCode());
        System.RefreshLocalizedText();
        SystemConfig.Save();
    }
}

/// <summary>
/// Base for pages whose content can be taller than the window.
/// Every page scrolls on its own so nothing is silently clipped at the bottom.
/// </summary>
public abstract class ScrollableTab : ITabItem
{
    public abstract string Name { get; }

    public bool Disabled => false;

    public void Draw()
    {
        using var content = ImRaii.Child($"{GetType().Name}_scroll", ImGui.GetContentRegionAvail(), false);
        if (!content) return;

        DrawContents();
    }

    protected abstract void DrawContents();
}

public class MapFunctionsTab : ScrollableTab
{
    public override string Name => Localization.Get("Tab.MapFunctions", "Map");

    protected override void DrawContents()
    {
        var configChanged = false;

        ImGuiTweaks.Header(Localization.Get("Header.ZoomOptions", "Zoom Options"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ScaleIconsWithZoom", "Scale icons with zoom"), ref System.SystemConfig.ScaleWithZoom);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ScaleTextWithZoom", "Scale text labels with zoom"), ref System.SystemConfig.ScaleTextWithZoom);

            ImGuiHelpers.ScaledDummy(5.0f);

            configChanged |= ImGuiTweaks.Checkbox(Localization.Get("Config.AutoZoom", "Auto Zoom"), ref System.SystemConfig.AutoZoom,
                Localization.Get("Config.AutoZoomTooltip", "Automatically sets zoom to a reasonable value relative to the map size."));
            configChanged |= ImGui.SliderFloat(Localization.Get("Config.AutoZoomScaleFactor", "Auto Zoom Scale Factor"), ref System.SystemConfig.AutoZoomScaleFactor, 0.20f, 1.00f);

            ImGuiHelpers.ScaledDummy(5.0f);

            configChanged |= ImGui.SliderFloat(Localization.Get("Config.ZoomSpeed", "Zoom Speed"), ref System.SystemConfig.ZoomSpeed, 0.02f, 0.500f);
            configChanged |= ImGui.SliderFloat(Localization.Get("Config.IconScale", "Icon Scale"), ref System.SystemConfig.IconScale, 0.10f, 3.0f);
        }

        ImGuiTweaks.Header(Localization.Get("Header.WhenOpeningMap", "When Opening Map"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox(Localization.Get("Config.FollowOnOpen", "Follow On Open"), ref System.SystemConfig.FollowOnOpen);

            ImGuiHelpers.ScaledDummy(5.0f);

            DrawCenterModeRadio();
        }

        ImGuiTweaks.Header(Localization.Get("Header.LinkBehaviors", "Link Behaviors"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox(Localization.Get("Config.CenterOnFlags", "Center on Flags"), ref System.SystemConfig.CenterOnFlag);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.CenterOnGathering", "Center on Gathering Areas"), ref System.SystemConfig.CenterOnGathering);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.CenterOnQuest", "Center on Quest"), ref System.SystemConfig.CenterOnQuest);
        }

        ImGuiTweaks.Header(Localization.Get("Header.MiscOptions", "Misc Options"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowMiscTooltips", "Show Misc Tooltips"), ref System.SystemConfig.ShowMiscTooltips);

            configChanged |= ImGui.Checkbox(Localization.Get("Config.LockMapOnCenter", "Lock Map on Center"), ref System.SystemConfig.LockCenterOnMap);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowOtherPlayers", "Show Other Players"), ref System.SystemConfig.ShowPlayers);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.DisableMapFocusOnAppear", "Disable Map Focus on Appear"), ref System.SystemConfig.NoFocusOnAppear);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.SuppressNativeMapSound", "Suppress the native map's open/close sound effect."),
                ref System.SystemConfig.SuppressNativeMapOpenSound);


            ImGuiHelpers.ScaledDummy(5.0f);

            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowTextLabels", "Show Text Labels"), ref System.SystemConfig.ShowTextLabels);
            configChanged |= ImGui.DragFloat(Localization.Get("Config.LargeLabelScale", "Large Label Scale"), ref System.SystemConfig.LargeAreaTextScale, 0.01f, 1.0f, 4.0f);
            configChanged |= ImGui.DragFloat(Localization.Get("Config.SmallLabelScale", "Small Label Scale"), ref System.SystemConfig.SmallAreaTextScale, 0.01f, 0.5f, 3.0f);

            ImGuiHelpers.ScaledDummy(5.0f);

            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowFogOfWar", "Show Fog of War"), ref System.SystemConfig.ShowFogOfWar);

            ImGuiHelpers.ScaledDummy(5.0f);

            configChanged |= ImGui.Checkbox(Localization.Get("Config.DebugMode", "Debug Mode"), ref System.SystemConfig.DebugMode);
        }

        ImGuiTweaks.Header(Localization.Get("Header.Toolbar", "Toolbar"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox(Localization.Get("Config.AlwaysShow", "Always Show"), ref System.SystemConfig.AlwaysShowToolbar);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowOnHover", "Show On Hover"), ref System.SystemConfig.ShowToolbarOnHover);

            ImGuiHelpers.ScaledDummy(5.0f);
            configChanged |= ImGui.DragFloat($"{Localization.Get("Config.Opacity", "Opacity")}##toolbar", ref System.SystemConfig.ToolbarFade, 0.01f, 0.0f, 1.0f);
        }

        ImGuiTweaks.Header(Localization.Get("Header.MapBrowser", "Map Browser"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowMapSidebar", "Show Map Browser Sidebar"), ref System.SystemConfig.ShowMapSidebar);

            ImGuiHelpers.ScaledDummy(5.0f);
            configChanged |= ImGui.DragFloat(Localization.Get("Config.MapSidebarWidth", "Sidebar Width"), ref System.SystemConfig.MapSidebarWidth, 1.0f, 160.0f, 500.0f);
        }

        ImGuiTweaks.Header(Localization.Get("Header.Coordinates", "Coordinates"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowCoordinateBar", "Show Coordinates"), ref System.SystemConfig.ShowCoordinateBar);

            ImGuiHelpers.ScaledDummy(5.0f);
            configChanged |= ImGuiTweaks.ColorEditWithDefault(Localization.Get("Config.TextColor", "Text Color"), ref System.SystemConfig.CoordinateTextColor, KnownColor.White.Vector());


            ImGuiHelpers.ScaledDummy(5.0f);
            configChanged |= ImGui.DragFloat($"{Localization.Get("Config.Opacity", "Opacity")}##coordinatebar", ref System.SystemConfig.CoordinateBarFade, 0.01f, 0.0f, 1.0f);

            ImGuiHelpers.ScaledDummy(10.0f);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowCoordinateGrid", "Show Coordinate Grid"), ref System.SystemConfig.ShowCoordinateGrid);

            ImGuiHelpers.ScaledDummy(5.0f);
            configChanged |= ImGuiTweaks.ColorEditWithDefault(Localization.Get("Config.GridColor", "Grid Color"), ref System.SystemConfig.CoordinateGridColor,
                KnownColor.White.Vector() with { W = 0.25f });
        }

        if (configChanged) {
            SystemConfig.Save();
        }
    }

    private void DrawCenterModeRadio()
    {
        var enumObject = System.SystemConfig.CenterOnOpen;
        var firstLine = true;

        foreach (Enum enumValue in Enum.GetValues(enumObject.GetType())) {
            if (!firstLine) ImGui.SameLine();

            if (ImGui.RadioButton(Localization.GetEnum(enumValue), enumValue.Equals(enumObject))) {
                System.SystemConfig.CenterOnOpen = (CenterTarget)enumValue;
                SystemConfig.Save();
            }

            firstLine = false;
        }

        ImGui.SameLine();
        ImGui.Text($"\t\t{Localization.Get("Config.CenterOnOpen", "Center on Open")}");
    }
}

public class StyleOptionsTab : ScrollableTab
{
    public override string Name => Localization.Get("Tab.Style", "Style");

    protected override void DrawContents()
    {
        var configChanged = false;

        ImGuiTweaks.Header(Localization.Get("Header.WindowOptions", "Window Options"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox(Localization.Get("Config.KeepOpen", "Keep Open"), ref System.SystemConfig.KeepOpen);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.LockWindowPosition", "Lock Window Position"), ref System.SystemConfig.LockWindow);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.HideWindowFrame", "Hide Window Frame"), ref System.SystemConfig.HideWindowFrame);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.HideWindowBackground", "Hide Window Background"), ref System.SystemConfig.HideWindowBackground);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.EnableShiftDrag", "Enable Shift + Drag to Move Window Frame"), ref System.SystemConfig.EnableShiftDragMove);
        }

        ImGuiTweaks.Header(Localization.Get("Header.WindowHiding", "Window Hiding"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox(Localization.Get("Config.HideWithGameGui", "Hide With Game GUI"), ref System.SystemConfig.HideWithGameGui);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.HideBetweenAreas", "Hide Between Areas"), ref System.SystemConfig.HideBetweenAreas);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.HideInCombat", "Hide in Combat"), ref System.SystemConfig.HideInCombat);
        }

        ImGuiTweaks.Header(Localization.Get("Header.WindowTitle", "Window Title"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowRegionText", "Show Region Text"), ref System.SystemConfig.ShowRegionLabel);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowMapText", "Show Map Text"), ref System.SystemConfig.ShowMapLabel);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowAreaText", "Show Area Text"), ref System.SystemConfig.ShowAreaLabel);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowSubAreaText", "Show Sub-Area Text"), ref System.SystemConfig.ShowSubAreaLabel);
        }

        ImGuiTweaks.Header(Localization.Get("Header.WindowLocation", "Window Location"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.DragFloat2(Localization.Get("Config.WindowPosition", "Window Position"), ref System.SystemConfig.WindowPosition);
            configChanged |= ImGui.DragFloat2(Localization.Get("Config.WindowSize", "Window Size"), ref System.SystemConfig.WindowSize);
        }

        ImGuiTweaks.Header(Localization.Get("Header.FadeOptions", "Fade Options"));
        using (ImRaii.PushIndent()) {
            using (var columns = ImRaii.Table("fade_options_toggles", 2)) {
                if (!columns) return;

                var value = System.SystemConfig.FadeMode;
                ImGui.TableNextColumn();

                foreach (Enum enumValue in Enum.GetValues(value.GetType())) {
                    var isFlagSet = value.HasFlag(enumValue);
                    if (ImGuiComponents.ToggleButton(enumValue.ToString(), ref isFlagSet)) {
                        var sourceValue = Convert.ToInt32(value);
                        var targetValue = Convert.ToInt32(enumValue);

                        if (value.HasFlag(enumValue)) {
                            System.SystemConfig.FadeMode = (FadeMode)Enum.ToObject(value.GetType(), sourceValue & ~targetValue);
                        }
                        else {
                            System.SystemConfig.FadeMode = (FadeMode)Enum.ToObject(value.GetType(), sourceValue | targetValue);
                        }

                        configChanged = true;
                    }

                    ImGui.SameLine();
                    ImGui.TextUnformatted(Localization.GetEnum(enumValue));

                    ImGui.TableNextColumn();
                }
            }

            configChanged |= ImGui.DragFloat(Localization.Get("Config.FadeOpacity", "Fade Opacity"), ref System.SystemConfig.FadePercent, 0.01f, 0.05f, 1.0f);
        }

        ImGuiTweaks.Header(Localization.Get("Header.AreaStyle", "Area Style"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGuiTweaks.ColorEditWithDefault(Localization.Get("Config.AreaColor", "Area Color"), ref System.SystemConfig.AreaColor,
                KnownColor.CornflowerBlue.Vector() with { W = 0.33f });
            configChanged |= ImGuiTweaks.ColorEditWithDefault(Localization.Get("Config.AreaOutlineColor", "Area Outline Color"), ref System.SystemConfig.AreaOutlineColor,
                KnownColor.CornflowerBlue.Vector() with { W = 0.30f });
        }

        ImGuiTweaks.Header(Localization.Get("Header.ConeOptions", "Cone Options"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ScalePlayerCone", "Scale Player Cone"), ref System.SystemConfig.ScalePlayerCone);

            ImGuiHelpers.ScaledDummy(5.0f);
            configChanged |= ImGui.DragFloat(Localization.Get("Config.ConeSize", "Cone Size"), ref System.SystemConfig.ConeSize, 0.25f);

            ImGuiHelpers.ScaledDummy(5.0f);
            configChanged |= ImGuiTweaks.ColorEditWithDefault(Localization.Get("Config.ConeColor", "Cone Color"), ref System.SystemConfig.PlayerConeColor,
                KnownColor.CornflowerBlue.Vector() with { W = 0.33f });
            configChanged |= ImGuiTweaks.ColorEditWithDefault(Localization.Get("Config.ConeOutlineColor", "Cone Outline Color"), ref System.SystemConfig.PlayerConeOutlineColor,
                KnownColor.CornflowerBlue.Vector() with { W = 1.00f });
        }

        ImGuiTweaks.Header(Localization.Get("Header.RadarOptions", "Radar Options"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowRadarRadius", "Show Radar Radius"), ref System.SystemConfig.ShowRadar);
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowRadarInDuties", "Show in Duties"), ref System.SystemConfig.ShowRadarInDuties);

            ImGuiHelpers.ScaledDummy(5.0f);

            configChanged |= ImGuiTweaks.ColorEditWithDefault(Localization.Get("Config.RadarAreaColor", "Radar Area Color"), ref System.SystemConfig.RadarColor,
                KnownColor.Gray.Vector() with { W = 0.10f });
            configChanged |= ImGuiTweaks.ColorEditWithDefault(Localization.Get("Config.RadarOutlineColor", "Radar Outline Color"), ref System.SystemConfig.RadarOutlineColor,
                KnownColor.Gray.Vector() with { W = 0.30f });
        }

        ImGuiTweaks.Header(Localization.Get("Header.PlayerIconOptions", "Player Icon Options"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGui.Checkbox(Localization.Get("Config.ShowPlayerIcon", "Show Player Icon"), ref System.SystemConfig.ShowPlayerIcon);

            ImGuiHelpers.ScaledDummy(5.0f);
            configChanged |= ImGui.DragFloat(Localization.Get("Config.PlayerIconSize", "Player Icon Size"), ref System.SystemConfig.PlayerIconScale, 0.05f);
        }

        if (configChanged) {
            if (System.MapWindow.SizeConstraints is { } constraints) {
                System.SystemConfig.WindowSize.X = MathF.Max(System.SystemConfig.WindowSize.X, constraints.MinimumSize.X);
                System.SystemConfig.WindowSize.Y = MathF.Max(System.SystemConfig.WindowSize.Y, constraints.MinimumSize.Y);
            }

            System.MapWindow.RefreshTitle();
            SystemConfig.Save();
        }
    }
}

public class NavigationTab : ScrollableTab
{
    public override string Name => Localization.Get("Tab.Navigation", "Navigation");

    protected override void DrawContents()
    {
        var configChanged = false;

        ImGuiTweaks.Header(Localization.Get("Header.AethernetNavigation", "Aethernet Teleporting"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGuiTweaks.Checkbox(Localization.Get("Config.EnableAethernetNavigation", "Use aethernet teleports"),
                ref System.SystemConfig.EnableAethernetNavigation,
                Localization.Get("Config.EnableAethernetNavigationTooltip",
                    "When navigating to a marker, teleport to the closest aethernet shard first and walk the rest instead of covering the whole distance on foot."));

            using (ImRaii.Disabled(!System.SystemConfig.EnableAethernetNavigation)) {
                ImGuiHelpers.ScaledDummy(5.0f);
                configChanged |= DrawDistanceSlider(Localization.Get("Config.AethernetSearchRadius", "Shard search radius"),
                    ref System.SystemConfig.AethernetSearchRadius, 50.0f, 500.0f,
                    Localization.Get("Config.AethernetSearchRadiusTooltip",
                        "A shard is only used when it is at least this close to the destination. Maps without an aethernet network simply have no shards nearby."));
            }
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGuiTweaks.Header(Localization.Get("Header.Mounting", "Mounting"));
        using (ImRaii.PushIndent()) {
            configChanged |= ImGuiTweaks.Checkbox(Localization.Get("Config.AutoMount", "Summon a mount for long trips"),
                ref System.SystemConfig.AutoMount,
                Localization.Get("Config.AutoMountTooltip",
                    "Asks for a mount before navigating, flying paths always need one. Areas that forbid mounting are skipped automatically."));

            using (ImRaii.Disabled(!System.SystemConfig.AutoMount)) {
                ImGuiHelpers.ScaledDummy(5.0f);
                configChanged |= DrawDistanceSlider(Localization.Get("Config.AutoMountDistance", "Mount from distance"),
                    ref System.SystemConfig.AutoMountDistance, 10.0f, 300.0f,
                    Localization.Get("Config.AutoMountDistanceTooltip",
                        "Shorter trips are faster on foot, the mount is only summoned beyond this distance."));

                ImGuiHelpers.ScaledDummy(5.0f);
                configChanged |= DrawMountSelector();
            }
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        using (ImRaii.PushIndent()) {
            ImGui.TextDisabled(Localization.Get("Config.NavigationRequiresVnavmesh", "Both options require the vnavmesh plugin."));
        }

        if (configChanged) SystemConfig.Save();
    }

    private static bool DrawDistanceSlider(string label, ref float value, float min, float max, string tooltip)
    {
        ImGui.SetNextItemWidth(-1.0f);
        var changed = ImGui.SliderFloat(label, ref value, min, max, "%.0f");

        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);

        return changed;
    }

    /// <summary>
    /// Mount list is read from the sheet once, unlocking a mount while the window is open is rare enough to ignore.
    /// </summary>
    private static List<(uint Id, string Name)>? unlockedMounts;

    private static bool DrawMountSelector()
    {
        var mounts = GetUnlockedMounts();
        var current = System.SystemConfig.MountId;

        var preview = Localization.Get("Config.MountAuto", "Automatic (roulette)");

        if (current is not 0)
        {
            foreach (var (id, name) in mounts)
            {
                if (id != current) continue;

                preview = name;

                break;
            }
        }

        var changed = false;

        ImGui.TextUnformatted(Localization.Get("Config.PreferredMount", "Preferred mount"));
        ImGui.SetNextItemWidth(-1.0f);

        using var combo = ImRaii.Combo("##preferredMount", preview);

        if (combo)
        {
            if (ImGui.Selectable(Localization.Get("Config.MountAuto", "Automatic (roulette)"), current is 0))
            {
                System.SystemConfig.MountId = 0;
                changed = true;
            }

            foreach (var (id, name) in mounts)
            {
                if (!ImGui.Selectable($"{name}##mount{id}", current == id)) continue;

                System.SystemConfig.MountId = id;
                changed = true;
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Localization.Get("Config.PreferredMountTooltip",
                "Mount summoned before long trips. Automatic lets the game pick any unlocked mount, which avoids per mount restrictions."));
        }

        return changed;
    }

    private static unsafe List<(uint Id, string Name)> GetUnlockedMounts()
    {
        if (unlockedMounts is not null) return unlockedMounts;

        var mounts = new List<(uint Id, string Name)>();

        var playerState = PlayerState.Instance();

        if (playerState is not null)
        {
            foreach (var mount in Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Mount>())
            {
                if (mount.RowId is 0) continue;
                if (!playerState->IsMountUnlocked((ushort)mount.RowId)) continue;

                var name = mount.Singular.ExtractText();
                if (name.Length is 0) continue;

                mounts.Add((mount.RowId, name));
            }
        }

        mounts.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

        unlockedMounts = mounts;

        return mounts;
    }
}

public class MarkerGroupsTab : ScrollableTab
{
    public override string Name => Localization.Get("Tab.MarkerGroups", "Marker Groups");

    protected override void DrawContents()
    {
        ImGuiTweaks.Header(Localization.Get("MarkerGroups.Header", "Custom Marker Groups"));

        using (ImRaii.PushIndent()) {
            ImGui.TextWrapped(Localization.Get("MarkerGroups.Description",
                "Only groups created here can be assigned to markers you place yourself. The built in groups belong to the marker data that ships with the plugin and are read only."));
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        var groups = System.CustomMarkerConfig.CustomGroups;

        if (groups.Count is 0) {
            ImGui.TextDisabled(Localization.Get("CustomGroup.Empty", "No custom groups yet"));
        }
        else {
            CustomMarkerGroup? removeTarget = null;

            var scale = ImGuiHelpers.GlobalScale;
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var deleteLabel = Localization.Get("CustomGroup.Delete", "Delete");
            var deleteWidth = ImGui.CalcTextSize(deleteLabel).X + ImGui.GetStyle().FramePadding.X * 2.0f + 4.0f;
            var iconIdWidth = 60.0f * scale;
            var previewSize = 20.0f * scale;
            var groupScaleWidth = 90.0f * scale;

            foreach (var group in groups) {
                using var id = ImRaii.PushId(group.Id);

                // First row, the display name and the delete button.
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - deleteWidth - spacing);
                if (ImGui.InputText("##groupName", ref group.Name, 64)) {
                    MarkerGroups.InvalidateCache();
                }

                // Writing the file on every keystroke would hit the disk dozens of times per edit.
                if (ImGui.IsItemDeactivatedAfterEdit()) {
                    System.CustomMarkerConfig.Save();
                }

                ImGui.SameLine();

                if (ImGui.Button(deleteLabel, new Vector2(deleteWidth, ImGui.GetFrameHeight()))) {
                    removeTarget = group;
                }

                // Second row, the icon and the size that every member of this group shares.
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(Localization.Get("CustomGroup.IconId", "Icon"));
                ImGui.SameLine();

                // No step buttons here, they would eat the whole field.
                var iconId = (int)Math.Min(group.IconId, IconHelper.MaxIconId);
                ImGui.SetNextItemWidth(iconIdWidth);
                if (ImGui.InputInt("##groupIcon", ref iconId)) {
                    group.IconId = IconHelper.Clamp(iconId);
                    MarkerGroups.InvalidateCache();
                }

                if (ImGui.IsItemDeactivatedAfterEdit()) {
                    System.CustomMarkerConfig.Save();
                }

                ImGui.SameLine();

                if (IconHelper.GetTexture(group.IconId) is { } texture) {
                    ImGui.Image(texture.Handle, new Vector2(previewSize, previewSize));
                }
                else {
                    ImGui.Dummy(new Vector2(previewSize, previewSize));
                }

                // The id does not exist, every marker of the group would be drawn without an icon.
                if (!IconHelper.IsValid(group.IconId)) {
                    ImGui.TextColored(KnownColor.Orange.Vector(), Localization.Get("CustomGroup.InvalidIconId", "This icon id does not exist."));
                }

                ImGui.SameLine();

                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(Localization.Get("CustomGroup.Scale", "Size"));
                ImGui.SameLine();

                ImGui.SetNextItemWidth(groupScaleWidth);
                if (ImGui.DragFloat("##groupScale", ref group.Scale, 0.01f, 0.1f, 5.0f)) {
                    MarkerGroups.InvalidateCache();
                }

                if (ImGui.IsItemDeactivatedAfterEdit()) {
                    System.CustomMarkerConfig.Save();
                }

                ImGui.Separator();
            }

            if (removeTarget is not null) {
                System.CustomMarkerConfig.RemoveCustomGroup(removeTarget);
            }
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.Button(Localization.Get("CustomGroup.Add", "New Group"), new Vector2(ImGui.GetContentRegionAvail().X, 24.0f * ImGuiHelpers.GlobalScale))) {
            System.CustomMarkerConfig.AddCustomGroup();
        }
    }
}

public class IconConfigurationTab : ITabItem
{
    public string Name => Localization.Get("Tab.Icons", "Icon Settings");

    public bool Disabled => false;

    private IconSetting? currentSetting;

    /// <summary>
    /// Icons used by user defined markers, they never show up in the global icon list.
    /// </summary>
    private static HashSet<uint> CollectCustomMarkerIconIds()
    {
        var iconIds = new HashSet<uint>();

        if (System.CustomMarkerConfig is null) return iconIds;

        foreach (var marker in System.CustomMarkerConfig.BuiltInMarkers) iconIds.Add(marker.DisplayIconId);
        foreach (var marker in System.CustomMarkerConfig.Markers) iconIds.Add(marker.DisplayIconId);

        iconIds.Remove(0);

        return iconIds;
    }

    public void Draw()
    {
        using (var leftChild = ImRaii.Child("left_child", new Vector2(48.0f * ImGuiHelpers.GlobalScale + ImGui.GetStyle().ItemSpacing.X, ImGui.GetContentRegionAvail().Y))) {
            if (leftChild) {
                using var selectionList = ImRaii.ListBox("iconSelection", ImGui.GetContentRegionAvail());

                // User defined markers and their groups carry their own icon, size and colour,
                // they are managed in the marker group tab instead of here.
                var customIconIds = CollectCustomMarkerIconIds();

                foreach (var (iconId, settings) in System.IconConfig.IconSettingMap.OrderBy(pairData => pairData.Key)) {
                    if (iconId is 0) continue;
                    if (customIconIds.Contains(iconId)) continue;
                    if (DrawHelpers.IsDisallowedIcon(iconId)) continue;

                    var texture = IconHelper.GetTexture(iconId);
                    var cursorStart = ImGui.GetCursorScreenPos();
                    if (ImGui.Selectable($"##iconSelect{iconId}", currentSetting == settings, ImGuiSelectableFlags.None, ImGuiHelpers.ScaledVector2(32.0f, 32.0f))) {
                        currentSetting = currentSetting == settings ? null : settings;
                    }

                    ImGui.SetCursorScreenPos(cursorStart);

                    if (texture is not null) {
                        ImGui.Image(texture.Handle, texture.Size / 2.0f * ImGuiHelpers.GlobalScale);
                    }
                    else {
                        ImGui.Dummy(ImGuiHelpers.ScaledVector2(16.0f, 16.0f));
                    }
                }
            }
        }

        ImGui.SameLine();

        using (var rightChild = ImRaii.Child("right_child", ImGui.GetContentRegionAvail(), false, ImGuiWindowFlags.NoScrollbar)) {
            if (rightChild) {
                if (currentSetting is null) {
                    using var textColor = ImRaii.PushColor(ImGuiCol.Text, KnownColor.Orange.Vector());

                    ImGui.SetCursorPosY(ImGui.GetContentRegionAvail().Y / 2.0f);
                    ImGuiHelpers.CenteredText(Localization.Get("Config.SelectIconToEdit", "Select an Icon to Edit Settings"));
                }
                else {
                    // Draw background texture
                    var settingsChanged = false;
                    var texture = IconHelper.GetTexture(currentSetting.IconId);
                    var smallestAxis = MathF.Min(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y);

                    if (ImGui.GetContentRegionAvail().X > ImGui.GetContentRegionAvail().Y) {
                        var remainingSpace = ImGui.GetContentRegionAvail().X - smallestAxis;
                        ImGui.SetCursorPosX(remainingSpace / 2.0f);
                    }

                    if (texture is not null) {
                        ImGui.Image(texture.Handle, new Vector2(smallestAxis, smallestAxis), Vector2.Zero, Vector2.One, new Vector4(1.0f, 1.0f, 1.0f, 0.20f));
                    }
                    else {
                        ImGui.Dummy(new Vector2(smallestAxis, smallestAxis));
                    }

                    ImGui.SetCursorPos(Vector2.Zero);

                    // Draw settings
                    ImGuiTweaks.Header(Localization.F("Header.ConfigureMarker", currentSetting.IconId));
                    using (ImRaii.PushIndent()) {
                        settingsChanged |= ImGui.Checkbox(Localization.Get("Config.HideIcon", "Hide Icon"), ref currentSetting.Hide);
                        settingsChanged |= ImGui.Checkbox(Localization.Get("Config.AllowTooltip", "Allow Tooltip"), ref currentSetting.AllowTooltip);
                        settingsChanged |= ImGui.Checkbox(Localization.Get("Config.AllowClick", "Allow Click Interaction"), ref currentSetting.AllowClick);

                        ImGuiHelpers.ScaledDummy(5.0f);
                        settingsChanged |= ImGuiTweaks.ColorEditWithDefault(Localization.Get("Config.Color", "Color"), ref currentSetting.Color, KnownColor.White.Vector());

                        ImGuiHelpers.ScaledDummy(5.0f);
                        settingsChanged |= ImGui.DragFloat(Localization.Get("Config.IconScale", "Icon Scale"), ref currentSetting.Scale, 0.01f, 0.05f, 20.0f);
                    }

                    ImGui.SetCursorPosY(ImGui.GetContentRegionMax().Y - 25.0f * ImGuiHelpers.GlobalScale);
                    if (ImGui.Button(Localization.Get("Config.ResetToDefault", "Reset to Default"), new Vector2(ImGui.GetContentRegionAvail().X, 25.0f * ImGuiHelpers.GlobalScale))) {
                        currentSetting.Reset();
                        System.IconConfig.Save();
                    }

                    if (settingsChanged) {
                        System.IconConfig.Save();
                    }
                }
            }
        }
    }
}
