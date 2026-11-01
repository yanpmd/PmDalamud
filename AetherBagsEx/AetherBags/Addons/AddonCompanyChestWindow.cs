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

namespace AetherBags.Addons;

/// <summary>
/// Replacement window for the free company chest. The chest has four item tabs,
/// each its own 50 slot container, so a tab bar sits under the header and only
/// the selected tab is scanned.
/// </summary>
public unsafe class AddonCompanyChestWindow : InventoryAddonBase
{
    private const float PageBarHeight = 30f;
    private const float PageButtonGap = 2f;
    private const uint PageButtonFontSize = 11;

    private static readonly Vector3 PageSelectedTint = new(0.95f, 0.62f, 0.10f);
    private static readonly Vector4 PageSelectedTextColor = new(0.10f, 0.07f, 0.02f, 1.0f);

    private readonly CompanyChestState _inventoryState = new();
    private readonly List<TextButtonNode> _pageButtons = new();

    /// <summary>Page of each button, kept in sync with <see cref="_pageButtons"/>.</summary>
    private readonly List<CompanyChestPage> _pageOrder = new();

    private TextNode? _gilNode;

    protected override InventoryStateBase InventoryState => _inventoryState;

    protected override bool HasFooter => false;
    protected override bool HasSlotCounter => true;

    private readonly Vector3 _tintColor = new(-4f / 255f, 6f / 255f, -12f / 255f);

    protected override InventoryWindowSizingLimits WindowSizingLimits => InventoryWindowSizingLimits.CompanyChest;
    protected override InventoryWindowSizingSettings WindowSizingSettings => System.Config.General.CompanyChestWindowSizing;

    protected override Vector2 ContentAreaPosition
        => ContentStartPosition + new Vector2(0f, PageBarHeight);

    protected override Vector2 ContentAreaSize
    {
        get
        {
            Vector2 size = ContentSize;
            return new Vector2(size.X, MathF.Max(0f, size.Y - PageBarHeight));
        }
    }

    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        EnsureValidPage();

        InitializeBackgroundDropTarget();

        WindowNode?.AddColor = _tintColor;

        CreatePageBar();

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

    /// <summary>
    /// Falls back to the first unlocked tab when the saved page is locked, which
    /// would otherwise leave the window permanently empty.
    /// </summary>
    private static void EnsureValidPage()
    {
        if (CompanyChestPages.IsAvailable(CompanyChestState.CurrentPage)) return;

        foreach (var item in CompanyChestPages.Items)
        {
            if (!CompanyChestPages.IsAvailable(item.page)) continue;

            CompanyChestState.CurrentPage = item.page;
            return;
        }
    }

    private void CreatePageBar()
    {
        // The addon object is reused across opens and its nodes are destroyed with
        // the old addon, so drop stale references and rebuild rather than reusing
        // dead nodes (touching them throws inside the native text node).
        _pageButtons.Clear();
        _pageOrder.Clear();

        foreach (var (page, _) in CompanyChestPages.Items)
        {
            // Locked chest tabs get no button at all: the company unlocks them by
            // rank, and showing a dead tab just looks like the plugin lost a page.
            if (!CompanyChestPages.IsAvailable(page)) continue;

            // Attach before touching the label: sizing a button forwards to its
            // label node, which needs a live native node.
            var button = new TextButtonNode
            {
                String = CompanyChestPages.GetName(page),
                OnClick = () => SwitchPage(page),
            };
            button.AttachNode(this);

            button.LabelNode.FontSize = PageButtonFontSize;
            button.Size = new Vector2(20f, PageBarHeight - 2f);

            _pageButtons.Add(button);
            _pageOrder.Add(page);
        }

        LayoutPageBar();
        UpdatePageButtonStates();
    }

    /// <summary>
    /// Spreads the chest tab buttons evenly across the window width and follows
    /// window resizing.
    /// </summary>
    private void LayoutPageBar()
    {
        int count = _pageButtons.Count;
        if (count == 0) return;

        const float gilWidth = 150f;

        float totalWidth = ContentSize.X;
        float barWidth = MathF.Max(0f, totalWidth - gilWidth);

        float buttonWidth = (barWidth - ((count - 1) * PageButtonGap)) / count;
        if (buttonWidth < 24f) buttonWidth = 24f;

        float x = ContentStartPosition.X;
        float y = ContentStartPosition.Y;

        for (int i = 0; i < count; i++)
        {
            _pageButtons[i].Position = new Vector2(x + (i * (buttonWidth + PageButtonGap)), y);
            _pageButtons[i].Size = new Vector2(buttonWidth, PageBarHeight - 2f);
        }

        if (_gilNode is not null)
        {
            _gilNode.Position = new Vector2(x + barWidth + 4f, y + 4f);
            _gilNode.Size = new Vector2(gilWidth - 8f, 18f);
        }
    }

    private void UpdatePageButtonStates()
    {
        var current = CompanyChestState.CurrentPage;

        for (int i = 0; i < _pageButtons.Count; i++)
        {
            bool selected = _pageOrder[i] == current;

            _pageButtons[i].BackgroundNode.AddColor = selected
                ? PageSelectedTint
                : Vector3.Zero;

            _pageButtons[i].LabelNode.TextColor = selected
                ? PageSelectedTextColor
                : ColorHelper.GetColor(50u);
        }
    }

    private void SwitchPage(CompanyChestPage page)
    {
        if (CompanyChestState.CurrentPage == page) return;
        if (!CompanyChestPages.IsAvailable(page)) return;

        CompanyChestState.CurrentPage = page;
        Util.SaveConfig(System.Config);

        UpdatePageButtonStates();

        _inventoryState.RefreshFromGame();
        RefreshCategoriesCore(autosize: true);
    }

    protected override void RefreshCategoriesCore(bool autosize)
    {
        if (!IsSetupComplete)
            return;

        // The highlight is refreshed by SwitchPage only; doing it here as well
        // would re-colour the tab buttons on every refresh.
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

        LayoutPageBar();

        Vector2 contentPos = ContentStartPosition;
        Vector2 contentSize = ContentSize;

        float footerY = contentPos.Y + contentSize.Y - FooterHeight + 4f;

        if (SlotCounterNode != null)
            SlotCounterNode.Position = new Vector2(contentPos.X + contentSize.X - 80f, footerY);
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        IsSetupComplete = false;

        _pageButtons.Clear();
        _pageOrder.Clear();
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
