using System;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Inventory.State;
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public unsafe class AddonSaddleBagWindow :  InventoryAddonBase
{
    private readonly SaddleBagState _inventoryState = new();
    private TextNode _slotCounterNode = null!;

    protected override InventoryStateBase InventoryState => _inventoryState;

    protected override bool HasFooter => false;
    protected override bool HasSlotCounter => true;

    private readonly Vector3 _tintColor = new (-16f / 255f, -4f / 255f, 8f / 255f);

    protected override InventoryWindowSizingLimits WindowSizingLimits => InventoryWindowSizingLimits.SaddleBag;
    protected override InventoryWindowSizingSettings WindowSizingSettings => System.Config.General.SaddleBagWindowSizing;

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        InitializeBackgroundDropTarget();

        WindowNode?.AddColor = _tintColor;

        ScrollableCategories = new InventoryScrollingAreaNode<WrappingGridNode<InventoryCategoryNodeBase>>
        {
            Position = ContentStartPosition,
            Size = ContentSize,
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

        _slotCounterNode = new TextNode
        {
            Position = new Vector2(Size.X - 10, 0),
            Size = new Vector2(82, 20),
            AlignmentType = AlignmentType.Right,
            FontType = FontType.MiedingerMed,
            TextFlags = TextFlags.Glare,
            TextColor = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(32)
        };
        _slotCounterNode.AttachNode(this);
        SlotCounterNode = _slotCounterNode;

        LayoutContent();

        _inventoryState.RefreshFromGame();

        IsSetupComplete = true;

        RefreshCategoriesCore(autosize: true);

        base.OnSetup(addon, atkValueSpan);
    }

    protected override void RefreshCategoriesCore(bool autosize)
    {
        if (!IsSetupComplete)
            return;

        _slotCounterNode.String = _inventoryState.GetEmptySlotsString();

        base.RefreshCategoriesCore(autosize);
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        IsSetupComplete = false;

        // Runs inside a reverse P/Invoke from the game's addon vtable: an exception
        // here kills the process, and during a reload the config may already be gone.
        try
        {
            if (System.Config?.General?.HideGameSaddleBags == true && Services.Framework is not null)
            {
                // Closing the vanilla addon from inside our own finalize re-enters the
                // shared addon vtable hook and crashes the game, so defer it a few ticks.
                Services.Framework.RunOnTick(CloseVanillaSaddleBag, delayTicks: 5);
            }
        }
        catch (Exception ex)
        {
            Services.Logger.Error(ex, "[SaddleBag] Failed to schedule the vanilla addon close");
        }

        base.OnFinalize(addon);
    }

    /// <summary>
    /// Restores and closes the hidden vanilla saddlebag. Runs on the next tick so
    /// it never re-enters the addon finalize hook while ours is still running.
    /// </summary>
    private static unsafe void CloseVanillaSaddleBag()
    {
        var unitManager = RaptureAtkUnitManager.Instance();
        if (unitManager == null) return;

        var saddleAddon = unitManager->GetAddonByName("InventoryBuddy");
        if (saddleAddon == null) return;

        saddleAddon->IsVisible = true;
        saddleAddon->Close(true);
    }
}
