using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Inventory.State;
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Enums;

namespace AetherBags.Addons;

/// <summary>
/// Replacement window for the free company chest. All chest tabs are shown in a
/// single list; deposits fill the pages in order without the player picking one.
/// </summary>
public unsafe class AddonCompanyChestWindow : InventoryAddonBase
{
    private readonly CompanyChestState _inventoryState = new();

    private TextNode? _gilNode;

    protected override InventoryStateBase InventoryState => _inventoryState;

    protected override bool HasFooter => false;
    protected override bool HasSlotCounter => true;

    private readonly Vector3 _tintColor = new(-4f / 255f, 6f / 255f, -12f / 255f);

    protected override InventoryWindowSizingLimits WindowSizingLimits => InventoryWindowSizingLimits.CompanyChest;
    protected override InventoryWindowSizingSettings WindowSizingSettings => System.Config.General.CompanyChestWindowSizing;

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
            Icon = CircleButtonIcon.GearCog,
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

        _gilNode = new TextNode
        {
            Size = new Vector2(140f, 18f),
            AlignmentType = AlignmentType.Right,
            FontType = FontType.MiedingerMed,
            TextFlags = TextFlags.Glare,
            TextColor = ColorHelper.GetColor(2),
            TextOutlineColor = ColorHelper.GetColor(32),
        };
        _gilNode.AttachNode(this);

        LayoutContent();

        _inventoryState.RefreshFromGame();

        IsSetupComplete = true;

        RefreshCategoriesCore(autosize: true);

        // The chest pages are only filled once the vanilla addon has finished
        // opening, so the first scan usually comes back empty. Re-scan shortly after.
        Services.Framework.RunOnTick(RescanAfterOpen, delayTicks: 5);

        base.OnSetup(addon, atkValueSpan);
    }

    /// <summary>
    /// Retries the scan once the game has populated the chest containers.
    /// </summary>
    private void RescanAfterOpen()
    {
        if (!IsSetupComplete) return;

        _inventoryState.RefreshFromGame();
        RefreshCategoriesCore(autosize: true);
    }

    protected override void OnShow(AtkUnitBase* addon)
    {
        base.OnShow(addon);

        _inventoryState.RefreshFromGame();
        RefreshCategoriesCore(autosize: true);
    }

    protected override void RefreshCategoriesCore(bool autosize)
    {
        if (!IsSetupComplete)
            return;

        if (SlotCounterNode is not null)
            SlotCounterNode.String = _inventoryState.GetEmptySlotsString();

        if (_gilNode is not null)
        {
            var manager = InventoryManager.Instance();
            if (manager is not null)
                _gilNode.String = Localization.F("CompanyChest.Gil", manager->GetFreeCompanyGil());
        }

        base.RefreshCategoriesCore(autosize);
    }

    protected override void LayoutContent()
    {
        base.LayoutContent();

        Vector2 contentPos = ContentStartPosition;
        Vector2 contentSize = ContentSize;

        float footerY = contentPos.Y + contentSize.Y - FooterHeight + 4f;

        if (SlotCounterNode != null)
            SlotCounterNode.Position = new Vector2(contentPos.X + contentSize.X - 80f, footerY);

        if (_gilNode is not null)
        {
            _gilNode.Position = new Vector2(contentPos.X + contentSize.X - 80f - 150f, footerY + 1f);
            _gilNode.Size = new Vector2(140f, 18f);
        }
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        IsSetupComplete = false;

        _gilNode = null;

        // Runs inside a reverse P/Invoke from the game's addon vtable: an exception
        // here kills the process, and during a reload the config may already be gone.
        try
        {
            if (System.Config?.General?.HideGameCompanyChest == true && Services.Framework is not null)
            {
                // Closing the vanilla addon from inside our own finalize re-enters the
                // shared addon vtable hook and crashes the game, so defer it a few ticks.
                Services.Framework.RunOnTick(CloseVanillaCompanyChest, delayTicks: 5);
            }
        }
        catch (Exception ex)
        {
            Services.Logger.Error(ex, "[CompanyChest] Failed to schedule the vanilla addon close");
        }

        base.OnFinalize(addon);
    }

    /// <summary>
    /// Restores and closes the hidden vanilla company chest. Runs on a later tick so
    /// it never re-enters the addon finalize hook while ours is still running.
    /// </summary>
    private static unsafe void CloseVanillaCompanyChest()
    {
        var unitManager = RaptureAtkUnitManager.Instance();
        if (unitManager == null) return;

        var chestAddon = unitManager->GetAddonByName("FreeCompanyChest");
        if (chestAddon == null) return;

        chestAddon->IsVisible = true;
        chestAddon->Close(true);
    }
}
