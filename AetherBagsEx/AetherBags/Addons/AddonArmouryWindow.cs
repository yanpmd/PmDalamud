using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Inventory.State;
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

/// <summary>
/// Replacement window for the game's armoury board ("ArmouryBoard"). A horizontal
/// part selector sits under the header, so only a single part (50 slots) is laid
/// out at a time.
/// </summary>
public unsafe class AddonArmouryWindow : InventoryAddonBase
{
    private const float PartBarHeight = 30f;
    private const float PartButtonGap = 2f;
    private const uint PartButtonFontSize = 11;

    /// <summary>Warm gold so the active part stands out against the dark frame.</summary>
    private static readonly Vector3 PartSelectedTint = new(0.95f, 0.62f, 0.10f);
    private static readonly Vector4 PartSelectedTextColor = new(0.10f, 0.07f, 0.02f, 1.0f);

    private readonly ArmouryState _armouryState = new();
    private readonly List<TextButtonNode> _partButtons = new();

    /// <summary>Last vanilla tab seen, so the poll only reacts to real changes.</summary>
    private int _lastVanillaTab = -1;

    protected override InventoryStateBase InventoryState => _armouryState;

    protected override bool HasFooter => false;
    protected override bool HasSlotCounter => true;

    private readonly Vector3 _tintColor = new(-12f / 255f, 6f / 255f, -10f / 255f);

    protected override InventoryWindowSizingLimits WindowSizingLimits => InventoryWindowSizingLimits.Armoury;
    protected override InventoryWindowSizingSettings WindowSizingSettings => System.Config.General.ArmouryWindowSizing;

    protected override Vector2 ContentAreaPosition
        => ContentStartPosition + new Vector2(0f, PartBarHeight);

    protected override Vector2 ContentAreaSize
    {
        get
        {
            Vector2 size = ContentSize;
            return new Vector2(size.X, MathF.Max(0f, size.Y - PartBarHeight));
        }
    }

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        // A fresh window always adopts the vanilla tab, so drop the value cached
        // by the previous session.
        _lastVanillaTab = -1;

        ApplyRequestedPart(atkValueSpan);

        // The setup values are not reliable for the initial part, so prefer the
        // tab the vanilla board is actually showing when it is still around.
        ApplyVanillaTab();

        InitializeBackgroundDropTarget();

        WindowNode?.AddColor = _tintColor;

        CreatePartBar();

        ScrollableCategories = new InventoryScrollingAreaNode<WrappingGridNode<InventoryCategoryNodeBase>>
        {
            Position = ContentAreaPosition,
            Size = ContentAreaSize,
            ContentHeight = 0f,
            AutoHideScrollBar = true,
        };
        ScrollableCategories.AttachNode(this);

        CategoriesNode = ScrollableCategories.ContentNode;
        CategoriesNode.HorizontalSpacing = CategorySpacing;
        CategoriesNode.VerticalSpacing = CategorySpacing;
        CategoriesNode.TopPadding = 4.0f;
        CategoriesNode.BottomPadding = 4.0f;

        var header = CalculateHeaderLayout(addon);

        SearchInputNode = new TextInputWithButtonNode
        {
            Position = header.SearchPosition,
            Size = header.SearchSize,
            OnInputReceived = _ => SearchDebouncer.Run(() => Services.Framework.RunOnTick(ItemRefresh)),
            OnButtonClicked = () => InventoryAddonContextMenu.OpenMain(this)
        };
        SearchInputNode.AttachNode(this);

        SettingsButtonNode = new CircleButtonNode
        {
            Position = new Vector2(header.HeaderWidth - SettingsButtonOffset, header.HeaderY),
            Size = new Vector2(28f),
            AddColor = _tintColor,
            Icon = ButtonIcon.GearCog,
            OnClick = System.AddonConfigurationWindow.Toggle
        };
        SettingsButtonNode.AttachNode(this);

        SlotCounterNode = new TextNode
        {
            Position = new Vector2(Size.X - 10, 0),
            Size = new Vector2(82, 20),
            AlignmentType = AlignmentType.Right,
            FontType = FontType.MiedingerMed,
            TextFlags = TextFlags.Glare,
            TextColor = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(32)
        };
        SlotCounterNode.AttachNode(this);

        LayoutContent();

        _armouryState.RefreshFromGame();
        IsSetupComplete = true;

        RefreshCategoriesCore(autosize: true);

        base.OnSetup(addon, atkValueSpan);
    }

    /// <summary>
    /// Re-reads the tab the vanilla armoury board is on. The board is only set up
    /// once, so later requests to open another part arrive as a plain Show and
    /// have to be picked up from the agent state.
    /// </summary>
    public void SyncPartFromGame()
    {
        var unitManager = RaptureAtkUnitManager.Instance();
        if (unitManager == null) return;

        var addon = unitManager->GetAddonByName("ArmouryBoard");
        if (addon == null) return;

        int tab = ((AddonArmouryBoard*)addon)->TabIndex;
        if (tab < 0) return;

        // Only follow actual changes of the vanilla tab. Comparing against the
        // current part instead would revert every manual switch made in this window.
        if (tab == _lastVanillaTab) return;
        _lastVanillaTab = tab;

        ArmouryPart? part = ArmouryParts.FromTabIndex(tab);
        if (part is null) return;

        if (ArmouryState.CurrentPart == part.Value) return;

        Services.Logger.DebugOnly($"[Armoury] Vanilla tab {tab} -> {part.Value}");

        ArmouryState.CurrentPart = part.Value;

        // While the window is still being set up the state change alone is enough:
        // OnSetup scans right after this and the category nodes do not exist yet.
        if (!IsOpen || !IsSetupComplete) return;

        UpdatePartButtonStates();
        _armouryState.RefreshFromGame();
        RefreshCategoriesCore(autosize: true);
    }

    /// <summary>
    /// The game passes the requested armoury tab as the first setup value, so
    /// clicking a gear slot on the character sheet opens that exact part instead
    /// of whatever was shown last time.
    /// </summary>
    private static void ApplyRequestedPart(Span<AtkValue> atkValueSpan)
    {
        if (atkValueSpan.Length == 0) return;

        ref AtkValue value = ref atkValueSpan[0];
        if (value.Type != AtkValueType.Int) return;

        ArmouryPart? part = ArmouryParts.FromTabIndex(value.Int);
        if (part is null) return;

        ArmouryState.CurrentPart = part.Value;
    }

    private void CreatePartBar()
    {
        // Buttons are owned by the addon and released with it, so a redundant
        // setup pass must not dispose (and then rebuild) live nodes.
        if (_partButtons.Count > 0) return;

        var parts = ArmouryParts.Items;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].part;

            var button = new TextButtonNode
            {
                Size = new Vector2(20f, PartBarHeight - 2f),
                String = ArmouryParts.GetShortName(part),
                OnClick = () => SwitchPart(part),
            };
            button.LabelNode.FontSize = PartButtonFontSize;

            button.AttachNode(this);
            _partButtons.Add(button);
        }

        LayoutPartBar();
        UpdatePartButtonStates();
    }

    /// <summary>
    /// Spreads the part buttons evenly across the window width. Runs on every
    /// layout pass so the bar follows window resizing.
    /// </summary>
    private void LayoutPartBar()
    {
        int count = _partButtons.Count;
        if (count == 0) return;

        float totalWidth = ContentSize.X;
        float buttonWidth = (totalWidth - ((count - 1) * PartButtonGap)) / count;
        if (buttonWidth < 24f) buttonWidth = 24f;

        float x = ContentStartPosition.X;
        float y = ContentStartPosition.Y;

        for (int i = 0; i < count; i++)
        {
            _partButtons[i].Position = new Vector2(x + (i * (buttonWidth + PartButtonGap)), y);
            _partButtons[i].Size = new Vector2(buttonWidth, PartBarHeight - 2f);
        }
    }

    private void UpdatePartButtonStates()
    {
        var parts = ArmouryParts.Items;
        var current = ArmouryState.CurrentPart;

        for (int i = 0; i < _partButtons.Count && i < parts.Length; i++)
        {
            bool selected = parts[i].part == current;

            _partButtons[i].BackgroundNode.AddColor = selected
                ? PartSelectedTint
                : Vector3.Zero;

            _partButtons[i].LabelNode.TextColor = selected
                ? PartSelectedTextColor
                : ColorHelper.GetColor(50u);
        }
    }

    private void SwitchPart(ArmouryPart part)
    {
        if (ArmouryState.CurrentPart == part) return;

        ArmouryState.CurrentPart = part;
        Util.SaveConfig(System.Config);

        UpdatePartButtonStates();

        _armouryState.RefreshFromGame();
        RefreshCategoriesCore(autosize: true);
    }

    private int _tabPollCounter;

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        base.OnUpdate(addon);

        // Clicking a gear slot on the character sheet can flip the vanilla board's
        // tab without raising a show or update event, so poll it. Every fifth frame
        // is fast enough to feel instant while keeping the lookup cost negligible.
        if (++_tabPollCounter < 5) return;
        _tabPollCounter = 0;

        SyncPartFromGame();
    }

    protected override void RefreshCategoriesCore(bool autosize)
    {
        if (!IsSetupComplete)
            return;

        // The part highlight is refreshed by SwitchPart / SyncPartFromGame only;
        // doing it here as well would re-colour 13 buttons on every refresh.
        if (SlotCounterNode is not null)
            SlotCounterNode.String = _armouryState.GetEmptySlotsString();

        base.RefreshCategoriesCore(autosize);
    }

    protected override void LayoutContent()
    {
        base.LayoutContent();

        LayoutPartBar();

        Vector2 contentPos = ContentStartPosition;
        Vector2 contentSize = ContentSize;

        float footerY = contentPos.Y + contentSize.Y - FooterHeight + 4f;

        if (SlotCounterNode != null)
            SlotCounterNode.Position = new Vector2(contentPos.X + contentSize.X - 80f, footerY);
    }

    /// <summary>
    /// Restores and closes the hidden vanilla armoury board. Runs on the next tick
    /// so it never re-enters the addon finalize hook while ours is still running.
    /// </summary>
    /// <summary>Copies the vanilla board's current tab into the window state.</summary>
    private unsafe void ApplyVanillaTab()
    {
        var unitManager = RaptureAtkUnitManager.Instance();
        if (unitManager == null) return;

        var addon = unitManager->GetAddonByName("ArmouryBoard");
        if (addon == null) return;

        int tab = ((AddonArmouryBoard*)addon)->TabIndex;
        if (tab < 0) return;

        _lastVanillaTab = tab;

        ArmouryPart? part = ArmouryParts.FromTabIndex(tab);
        if (part is null) return;

        ArmouryState.CurrentPart = part.Value;
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        IsSetupComplete = false;
        _lastVanillaTab = -1;

        _partButtons.Clear();

        // The vanilla board is only ever hidden, never closed from here. Closing it
        // while our own addon is being finalized races the game's addon teardown and
        // crashes inside the native finalizer that walks the node tree.
        base.OnFinalize(addon);
    }
}
