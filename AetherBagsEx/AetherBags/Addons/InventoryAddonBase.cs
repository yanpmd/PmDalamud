using System;
using System.Collections.Generic;
using System.Numerics;
using AetherBags.Configuration;
using AetherBags.Helpers;
using AetherBags.Hooks;
using AetherBags.Inventory;
using AetherBags.Inventory.Categories;
using AetherBags.Inventory.Context;
using AetherBags.Inventory.Items;
using AetherBags.Inventory.Scanning;
using AetherBags.Inventory.State;
using AetherBags.IPC.ExternalCategorySystem;
using AetherBags.Monitoring;
using AetherBags.Nodes.Input;
using AetherBags.Nodes.Inventory;
using AetherBags.Nodes.Layout;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.ContextMenu;
using KamiToolKit.Nodes;

namespace AetherBags.Addons;

public abstract unsafe class InventoryAddonBase : NativeAddon, IInventoryWindow
{
    protected readonly InventoryCategoryHoverCoordinator HoverCoordinator = new();
    protected readonly InventoryCategoryPinCoordinator PinCoordinator = new();
    protected readonly HashSet<InventoryCategoryNode> HoverSubscribed = new();

    protected DragDropNode BackgroundDropTarget = null!;
    protected InventoryScrollingAreaNode<WrappingGridNode<InventoryCategoryNodeBase>> ScrollableCategories = null!;
    protected WrappingGridNode<InventoryCategoryNodeBase> CategoriesNode = null!;
    protected TextInputWithButtonNode SearchInputNode = null!;
    protected InventoryFooterNode FooterNode = null!;
    protected TextNode? SlotCounterNode { get; set; }
    protected CircleButtonNode SettingsButtonNode = null!;

    internal ContextMenu ContextMenu = null!;

    protected readonly SharedNodePool<InventoryDragDropNode> SharedItemNodePool = new(
        maxSize: 256,
        factory: null,
        resetAction: node => node.ResetForReuse());

    protected readonly SharedNodePool<InventoryCategoryNode> SharedCategoryNodePool = new(
        maxSize: 32,
        factory: null,
        resetAction: node => node.ResetForReuse());

    protected readonly VirtualizationState CategoryVirtualization = new() { BufferSize = 200f };

    protected readonly Debouncer SearchDebouncer = new(System.Config.General.SearchDelay);

    protected virtual InventoryWindowSizingLimits WindowSizingLimits => InventoryWindowSizingLimits.Inventory;
    protected virtual float MinWindowWidth => WindowSizingLimits.SafeMinWidth;
    protected virtual float MaxWindowWidth => WindowSizingLimits.DefaultMaxWidth;
    protected virtual float MinWindowHeight => WindowSizingLimits.SafeMinHeight;
    protected virtual float MaxWindowHeight => WindowSizingLimits.DefaultMaxHeight;
    protected virtual InventoryWindowSizingSettings WindowSizingSettings => System.Config.General.InventoryWindowSizing;

    // Layout metrics now come from the active style preset, see StylePresets.
    protected static float CategorySpacing => StylePresets.Current.CategorySpacing;
    protected static float ItemSize => StylePresets.Current.ItemWidth;
    protected static float ItemPadding => StylePresets.Current.ItemPadding;
    protected static float FooterHeight => StylePresets.Current.FooterHeight;
    protected const float FooterTopSpacing = 4f;
    protected const float SettingsButtonOffset = 62f;
    protected const float ScrollBarWidth = 16f;
    protected const float ContentHeightOffset = 4f;

    protected bool RefreshQueued;
    protected bool RefreshAutosizeQueued;
    protected bool IsSetupComplete;
    private bool _deferredPopulationInProgress;
    private bool _initialPopulationComplete;
    private const int ItemsPerFrame = 40;

    protected abstract InventoryStateBase InventoryState { get; }

    protected virtual bool HasFooter => true;
    protected virtual bool HasPinning => true;
    protected virtual bool HasSlotCounter => false;

    private readonly HashSet<uint> _searchMatchScratch = new();
    private bool _isRefreshing;
    private string _lastSearchText = string.Empty;

    private int _requestedUpdateCount;
    private int _refreshFromLifecycleCount;
    private long _lastLogTick;
    private long _lastRefreshTick;
    private long _lastSeenBulkCommitVersion;

    /// <summary>
    /// The addon asks for an update on every frame. A full inventory scan,
    /// category rebuild and window resize at 60fps is wasted work, so the
    /// periodic refresh is throttled. Inventory changes are still picked up
    /// immediately through <see cref="RefreshFromLifecycle"/>, and this only
    /// needs to be fast enough to catch changes made outside of it (other
    /// plugins, the game's own UI, ...).
    /// </summary>
    private const int MinRefreshIntervalMs = 100;

    public void ManualRefresh() => ExecuteRefresh(true);

    /// <summary>True when this window displays the saddlebag containers.</summary>
    public bool IsSaddleBagWindow => InventoryState.SourceType
        is InventorySourceType.AllSaddleBags or InventorySourceType.SaddleBag or InventorySourceType.PremiumSaddleBag;

    /// <summary>True when this window displays the player's main bags.</summary>
    public bool IsMainBagsWindow => InventoryState.SourceType == InventorySourceType.MainBags;

    /// <summary>True when this window displays the active retainer's pages.</summary>
    public bool IsRetainerWindow => InventoryState.SourceType == InventorySourceType.Retainer;

    /// <summary>True when this window displays the armoury chest.</summary>
    public bool IsArmouryWindow => InventoryState.SourceType == InventorySourceType.Armoury;

    public string GetSearchText() => SearchInputNode?.SearchString.ExtractText() ?? string.Empty;

    public InventoryStats GetStats() => InventoryState.GetStats();

    public IReadOnlyList<CategorizedInventory>? GetVisibleCategories()
    {
        if (!IsSetupComplete) return null;
        string filter = GetSearchText();
        return InventoryState.GetCategories(filter);
    }

    public virtual void SetSearchText(string searchText)
    {
        Services.Framework.RunOnTick(() =>
        {
            if (IsOpen) SearchInputNode.SearchString = searchText;
            RefreshCategoriesCore(autosize: true);
        }, delayTicks: 3);
    }

    public void FocusSearch()
    {
        Services.Framework.RunOnTick(() =>
        {
            AtkUnitBase* addon = this;
            if (!IsOpen || SearchInputNode == null || SearchInputNode.FocusNode == null || addon == null) return;

            SearchInputNode.SetInputFocus();
        }, delayTicks: 2);
    }

    public bool TryFocusSearch()
    {
        AtkUnitBase* addon = this;
        if (!IsOpen || SearchInputNode == null || SearchInputNode.FocusNode == null || addon == null) return false;
        if (!IsFocusedAddon(addon)) return false;

        SearchInputNode.SetInputFocus();
        return true;
    }

    private void ExecuteRefresh(bool autosize)
    {
        if (!IsSetupComplete || !IsOpen || _isRefreshing) return;

        LogRefreshStats();

        try
        {
            _isRefreshing = true;
            InventoryState.RefreshFromGame();
            System.LootedItemsTracker.FlushPendingChanges();
            ApplyPendingBulkCompact();
            RefreshCategoriesCore(autosize);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public void RefreshFromLifecycle()
    {
        _refreshFromLifecycleCount++;
        ExecuteRefresh(autosize: true);
    }

    /// <summary>
    /// A bulk commit (looting a full chest, moving an entire page, ...) changes far more
    /// items than a single pickup and leaves the compact layout with gaps for the rest of
    /// the session. "Compact once per open" would only tidy that up on the next reopen, so
    /// re-arm the gate once for every bulk commit. Single pickups are ignored deliberately:
    /// re-flowing for them would reshuffle items while the cursor is still moving.
    /// </summary>
    private void ApplyPendingBulkCompact()
    {
        long version = System.LootedItemsTracker.BulkCommitVersion;

        if (version == _lastSeenBulkCommitVersion)
            return;

        _lastSeenBulkCommitVersion = version;

        CategoriesNode?.ResetCompactPackingGate();
        CategoriesNode?.InvalidateLayout();
    }

    protected virtual void RefreshCategoriesCore(bool autosize)
    {
        if (!IsSetupComplete)
            return;

        var config = System.Config.General;
        string searchText = SearchInputNode.SearchString.ExtractText();
        bool isSearching = !string.IsNullOrWhiteSpace(searchText);

        if (searchText != _lastSearchText)
        {
            _lastSearchText = searchText;
            System.AetherBagsAPI?.API.RaiseSearchChanged(searchText);
        }

        IReadOnlyList<CategorizedInventory>? allData = null;

        if (config.SearchMode == SearchMode.Highlight && isSearching)
        {
            allData = InventoryState.GetCategories(string.Empty);
            RebuildSearchHighlight(allData, searchText);
        }
        else
        {
            HighlightState.ClearFilter(HighlightSource.Search);
        }

        if (SearchInputNode != null)
        {
            bool atActive = !string.IsNullOrEmpty(HighlightState.SelectedAllaganToolsFilterKey);

            SearchInputNode.HintAddColor = (atActive)
                ? new Vector3(0.0f, 0.3f, 0.3f)
                : Vector3.Zero;
        }

        if (HasFooter)
        {
            FooterNode.SlotAmountText = InventoryState.GetEmptySlotsString();
            FooterNode.RefreshCurrencies();
        }

        string dataFilter = config.SearchMode == SearchMode.Filter ? searchText : string.Empty;
        // In highlight mode the unfiltered list was already built above.
        var categories = allData ?? InventoryState.GetCategories(dataFilter);

        float maxContentWidth = CategoriesNode.Width > 0 ? CategoriesNode.Width : ContentSize.X;
        int maxItemsPerLine = CalculateOptimalItemsPerLine(maxContentWidth);

        bool deferItems = config.FrameBatchingEnabled && !_deferredPopulationInProgress && !_initialPopulationComplete;

        bool categoriesChanged = CategoriesNode.SyncWithListDataByKey<CategorizedInventory, InventoryCategoryNode, uint>(
            dataList: categories,
            getKeyFromData: categorizedInventory => categorizedInventory.Key,
            getKeyFromNode: node => node.CategorizedInventory.Key,
            updateNode: (node, data) =>
            {
                node.SetCategoryData(data, Math.Min(data.Items.Count, maxItemsPerLine), deferItemCreation: deferItems);
                if (!deferItems) node.RefreshNodeVisuals();
            },
            createNodeMethod: _ => CreateCategoryNode(),
            resetNodeForReuse: ResetCategoryNodeForReuse,
            externalPool: SharedCategoryNodePool);

        if (HasPinning)
        {
            bool pinsChanged = PinCoordinator.ApplyPinnedStates(CategoriesNode);
            if (pinsChanged) HoverCoordinator.ResetAll(CategoriesNode);
        }

        WireHoverHandlers();

        // Forcing a reflow every refresh defeats the layout cache (and the
        // compact packing fast path), so only invalidate when the set or order
        // of the categories actually changed.
        if (categoriesChanged)
            CategoriesNode.InvalidateLayout();

        if (autosize)
            AutoSizeWindow();
        else
        {
            LayoutContent();
            CategoriesNode.RecalculateLayout();
        }

        if (deferItems && !_deferredPopulationInProgress)
        {
            StartDeferredItemPopulation();
        }
        else if (!deferItems && !_initialPopulationComplete)
        {
            _initialPopulationComplete = true;
        }

        System.AetherBagsAPI?.API.RaiseCategoriesRefreshed();
    }

    private void StartDeferredItemPopulation()
    {
        _deferredPopulationInProgress = true;
        Services.Framework.RunOnTick(PopulateCategoryBatch, delayTicks: 1);
    }

    private void PopulateCategoryBatch()
    {
        if (!IsOpen)
        {
            _deferredPopulationInProgress = false;
            return;
        }

        UpdateCategoryVisibility();

        int itemsPopulated = 0;
        using (CategoriesNode.DeferRecalculateLayout())
        {
            var nodes = CategoriesNode.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is not InventoryCategoryNode categoryNode || !categoryNode.NeedsItemPopulation)
                    continue;

                if (!CategoryVirtualization.IsVisible(i))
                    continue;

                int categoryItemCount = categoryNode.CategorizedInventory.Items.Count;

                if (itemsPopulated > 0 && itemsPopulated + categoryItemCount > ItemsPerFrame)
                    break;

                categoryNode.PopulateItems();
                categoryNode.RefreshNodeVisuals();
                itemsPopulated += categoryItemCount;

                if (itemsPopulated >= ItemsPerFrame)
                    break;
            }

            if (itemsPopulated < ItemsPerFrame)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] is not InventoryCategoryNode categoryNode || !categoryNode.NeedsItemPopulation)
                        continue;

                    if (CategoryVirtualization.IsVisible(i))
                        continue;

                    int categoryItemCount = categoryNode.CategorizedInventory.Items.Count;

                    if (itemsPopulated > 0 && itemsPopulated + categoryItemCount > ItemsPerFrame)
                        break;

                    categoryNode.PopulateItems();
                    categoryNode.RefreshNodeVisuals();
                    itemsPopulated += categoryItemCount;

                    if (itemsPopulated >= ItemsPerFrame)
                        break;
                }
            }
        }

        bool hasMore = false;
        foreach (var node in CategoriesNode.Nodes)
        {
            if (node is InventoryCategoryNode categoryNode && categoryNode.NeedsItemPopulation)
            {
                hasMore = true;
                break;
            }
        }

        if (hasMore)
        {
            Services.Framework.RunOnTick(PopulateCategoryBatch);
        }
        else
        {
            _deferredPopulationInProgress = false;
            _initialPopulationComplete = true;
        }
    }

    protected readonly struct HeaderLayout
    {
        public Vector2 SearchPosition { get; init; }
        public Vector2 SearchSize { get; init; }
        public float HeaderWidth { get; init; }
        public float HeaderY { get; init; }
    }

    /// <summary>
    /// Width reserved on the left of the window header for window specific
    /// controls. The search bar stays centered in whatever space is left.
    /// </summary>
    protected virtual float HeaderLeftReserve => 0f;

    protected HeaderLayout CalculateHeaderLayout(AtkUnitBase* addon)
    {
        var header = addon->WindowHeaderCollisionNode;
        float headerW = header->Width;

        const float searchHeight = 28f;
        float itemY = header->Y + (header->Height - searchHeight) * 0.5f;

        // The bag windows no longer render a window title, so nothing has to be
        // reserved on the left unless the window puts its own control there.
        float titleReserve = HeaderLeftReserve;
        const float gap = 8f;
        float leftReserve = titleReserve + gap;
        float rightReserve = SettingsButtonOffset + gap;

        const float minSearchWidth = 200f;
        const float maxSearchWidth = 360f;

        float available = headerW - rightReserve - leftReserve;
        float searchWidth = Math.Clamp(
            headerW * 0.45f,
            minSearchWidth,
            Math.Max(minSearchWidth, Math.Min(maxSearchWidth, available)));

        float centeredX = (headerW - searchWidth) * 0.5f;
        float minX = leftReserve;
        float maxX = headerW - rightReserve - searchWidth;
        float searchX = Math.Clamp(centeredX, minX, Math.Max(minX, maxX));

        return new HeaderLayout
        {
            SearchPosition = new Vector2(searchX, itemY),
            SearchSize = new Vector2(searchWidth, searchHeight),
            HeaderWidth = headerW,
            HeaderY = itemY
        };
    }

    protected void InitializeBackgroundDropTarget()
    {
        BackgroundDropTarget = new DragDropNode
        {
            Position = ContentStartPosition,
            Size = ContentSize,
            IconId = 0,
            IsDraggable = false,
            IsClickable = false,
            // Everything = catch-all so Crystal / EventItem payloads from our other windows
            // (or vanilla) reach OnBackgroundPayloadAccepted; the handler branches per payload
            // type and rejects anything we don't know how to route.
            AcceptedType = DragDropType.Everything,
        };

        BackgroundDropTarget.DragDropBackgroundNode.IsVisible = false;
        BackgroundDropTarget.IconNode.IsVisible = false;

        BackgroundDropTarget.OnPayloadAccepted = OnBackgroundPayloadAccepted;

        BackgroundDropTarget.AttachNode(this);
    }

    protected virtual InventoryCategoryNode CreateCategoryNode()
    {
        var node = SharedCategoryNodePool.TryRent();
        if (node == null)
        {
            node = new InventoryCategoryNode
            {
                Size = ContentSize with { Y = 120 },
                SharedItemPool = SharedItemNodePool,
            };
        }

        node.OnRefreshRequested = ManualRefresh;
        node.OnDragEnd = () => InventoryOrchestrator.RefreshAll(updateMaps: true);
        node.OnHeaderRightClick = categoryNode => InventoryAddonContextMenu.OpenCategoryActions(this, categoryNode.CategorizedInventory);
        node.SharedItemPool = SharedItemNodePool;
        node.ApplyStyle(StylePresets.Current);
        return node;
    }

    private static void ResetCategoryNodeForReuse(InventoryCategoryNode node)
    {
        node.ResetForReuse();
    }

    private void OnBackgroundPayloadAccepted(DragDropNode node, DragDropPayload acceptedPayload)
    {
        if (!acceptedPayload.IsValidInventoryPayload) return;

        Services.Logger.Information($"[BackgroundDrop] enter: payloadType={acceptedPayload.Type}, payload=({acceptedPayload.Int1}@{acceptedPayload.Int2}), windowSource={InventoryState.SourceType}");

        var mapped = InventoryType.GetInventoryTypeFromContainerId(acceptedPayload.Int1);
        InventoryType srcContainer = mapped != 0 ? mapped : (InventoryType)acceptedPayload.Int1;
        ushort srcSlot = (ushort)acceptedPayload.Int2;

        if (acceptedPayload.Type == DragDropType.Crystal)
        {
            if (InventoryState.SourceType == InventorySourceType.Retainer
                && srcContainer == InventoryType.Crystals)
            {
                if (RetainerCommands.TryEntrust(srcContainer, srcSlot))
                {
                    ManualRefresh();
                    return;
                }
            }
            else if (InventoryState.SourceType == InventorySourceType.MainBags
                     && srcContainer == InventoryType.RetainerCrystals)
            {
                if (RetainerCommands.TryRetrieve(srcContainer, srcSlot))
                {
                    ManualRefresh();
                    return;
                }
            }
        }

        InventoryLocation emptyLocation = InventoryState.GetFirstEmptySlot();

        if (!emptyLocation.IsValid)
        {
            Services.Logger.Error("No empty slots available to receive drop.");
            return;
        }

        InventoryMappedLocation visualLocation = InventoryContextState.GetVisualLocation(emptyLocation.Container, emptyLocation.Slot);

        var visualInvType = InventoryType.GetInventoryTypeFromContainerId(visualLocation.Container);
        int absoluteIndex = visualInvType.GetInventoryStartIndex + visualLocation.Slot;

        var targetPayload = new DragDropPayload
        {
            Type = DragDropType.Item,
            Int1 = visualLocation.Container,
            Int2 = visualLocation.Slot,
            ReferenceIndex = (short)absoluteIndex
        };

        Services.Logger.DebugOnly($"[BackgroundDrop] Target: {emptyLocation} -> Visual: {visualLocation} (Ref: {absoluteIndex})");

        InventoryMoveHelper.HandleItemMovePayload(acceptedPayload, targetPayload);

        ManualRefresh();
    }

    protected void WireHoverHandlers()
    {
        var nodes = CategoriesNode.Nodes;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not InventoryCategoryNode node)
                continue;

            if (!HoverSubscribed.Add(node))
                continue;

            node.OnHeaderHoverChanged += (src, hovering) =>
            {
                HoverCoordinator.OnCategoryHoverChanged(CategoriesNode, src, hovering);
            };
        }
    }

    protected int CalculateOptimalItemsPerLine(float availableWidth)
        => Math.Clamp((int)MathF.Floor((availableWidth + ItemPadding) / (ItemSize + ItemPadding)), 1, 15);

    private readonly struct EffectiveWindowSizing
    {
        public InventoryWindowSizingMode Mode { get; init; }
        public float FixedWidth { get; init; }
        public float FixedHeight { get; init; }
        public float MinWidth { get; init; }
        public float MaxWidth { get; init; }
        public float MinHeight { get; init; }
        public float MaxHeight { get; init; }
    }

    private EffectiveWindowSizing GetEffectiveWindowSizing()
    {
        var settings = WindowSizingSettings;
        InventoryWindowSizingDefaults.Normalize(settings, WindowSizingLimits);

        return settings.Mode switch
        {
            InventoryWindowSizingMode.Fixed => new EffectiveWindowSizing
            {
                Mode = settings.Mode,
                FixedWidth = settings.FixedWidth,
                FixedHeight = settings.FixedHeight,
                MinWidth = MinWindowWidth,
                MaxWidth = InventoryWindowSizingDefaults.MaxConfigurableWidth,
                MinHeight = MinWindowHeight,
                MaxHeight = InventoryWindowSizingDefaults.MaxConfigurableHeight,
            },
            InventoryWindowSizingMode.CustomBounds => new EffectiveWindowSizing
            {
                Mode = settings.Mode,
                MinWidth = settings.MinWidth,
                MaxWidth = settings.MaxWidth,
                MinHeight = settings.MinHeight,
                MaxHeight = settings.MaxHeight,
            },
            _ => new EffectiveWindowSizing
            {
                Mode = InventoryWindowSizingMode.Automatic,
                MinWidth = MinWindowWidth,
                MaxWidth = MaxWindowWidth,
                MinHeight = MinWindowHeight,
                MaxHeight = MaxWindowHeight,
            }
        };
    }

    /// <summary>
    /// Area inside the window that holds the category grid. Windows that put a
    /// sidebar next to the grid narrow this instead of moving the grid themselves.
    /// </summary>
    protected virtual Vector2 ContentAreaPosition => ContentStartPosition;

    protected virtual Vector2 ContentAreaSize => ContentSize;

    protected virtual void LayoutContent()
    {
        Vector2 contentPos = ContentAreaPosition;
        Vector2 contentSize = ContentAreaSize;

        float footerH = HasFooter || HasSlotCounter ?  FooterHeight : 0;

        if (HasFooter)
        {
            FooterNode.Position = new Vector2(contentPos.X, contentPos.Y + contentSize.Y - footerH);
            FooterNode.Size = new Vector2(contentSize.X, footerH);
        }
        else if (HasSlotCounter && SlotCounterNode != null)
        {
            SlotCounterNode.Position = new Vector2(contentSize.X -80f, contentPos.Y + contentSize.Y - footerH + 4f);
        }

        float gridH = contentSize.Y - ((HasFooter || HasSlotCounter) ? FooterHeight + FooterTopSpacing : 0);
        if (gridH < 0) gridH = 0;

        ScrollableCategories.Position = contentPos;
        ScrollableCategories.Size = new Vector2(contentSize.X, gridH);

        float categoriesWidth = contentSize.X - ScrollBarWidth;
        CategoriesNode.Width = categoriesWidth;

        UpdateCategoryMaxWidths(categoriesWidth);
    }

    private void UpdateCategoryMaxWidths(float maxWidth)
    {
        // In single column mode every category is stretched to the full content
        // width so the wrapping grid can only fit one of them per row.
        bool stretchCategories = StylePresets.Current.LayoutMode == CategoryLayoutMode.SingleColumn;

        foreach (var node in CategoriesNode.Nodes)
        {
            if (node is not InventoryCategoryNodeBase categoryNode) continue;

            if (node is InventoryCategoryNode inventoryCategoryNode)
            {
                float? desiredFixedWidth = stretchCategories ? maxWidth : null;
                if (inventoryCategoryNode.FixedWidth != desiredFixedWidth)
                    inventoryCategoryNode.FixedWidth = desiredFixedWidth;
            }

            if (categoryNode.MaxWidth != maxWidth)
            {
                categoryNode.MaxWidth = maxWidth;
                categoryNode.RecalculateSize();
            }
        }
    }

    protected virtual void AutoSizeWindow()
    {
        var sizing = GetEffectiveWindowSizing();

        if (sizing.Mode == InventoryWindowSizingMode.Fixed)
        {
            ResizeWindow(sizing.FixedWidth, sizing.FixedHeight, recalcLayout: true);
            UpdateScrollParameters();
            return;
        }

        var nodes = CategoriesNode.Nodes;

        float maxChildWidth = 0f;
        int childCount = 0;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not InventoryCategoryNodeBase cat)
                continue;

            childCount++;
            float w = cat.Width;
            if (w > maxChildWidth) maxChildWidth = w;
        }

        if (childCount == 0)
        {
            ResizeWindow(sizing.MinWidth, sizing.MinHeight, recalcLayout: true);
            UpdateScrollParameters();
            return;
        }

        float footerSpace = HasFooter || HasSlotCounter ? FooterHeight + FooterTopSpacing : 0;

        float requiredWidth = maxChildWidth + ScrollBarWidth + ContentAreaPosition.X + ContentStartPosition.X;
        float finalWidth = Math.Clamp(requiredWidth, sizing.MinWidth, sizing.MaxWidth);

        if (SettingsButtonNode != null)
        {
            SettingsButtonNode.X = finalWidth - SettingsButtonOffset;
        }

        float contentWidth = finalWidth - ContentAreaPosition.X - ContentStartPosition.X;
        float categoriesWidth = contentWidth - ScrollBarWidth;

        CategoriesNode.Width = categoriesWidth;
        UpdateCategoryMaxWidths(categoriesWidth);
        CategoriesNode.RecalculateLayout();

        float requiredGridHeight = CategoriesNode.GetRequiredHeight();

        float requiredContentHeight = requiredGridHeight + footerSpace;

        // Use the actual content area top so window specific bands (the armoury
        // part bar) are accounted for and the grid is not clipped into a scrollbar.
        float requiredWindowHeight = requiredContentHeight + ContentAreaPosition.Y + ContentStartPosition.X + ContentHeightOffset;
        float finalHeight = Math.Clamp(requiredWindowHeight, sizing.MinHeight, sizing.MaxHeight);

        ResizeWindow(finalWidth, finalHeight, recalcLayout: false);

        UpdateScrollParameters();
    }

    protected void UpdateScrollParameters()
    {
        if (ScrollableCategories == null) return;

        float requiredHeight = CategoriesNode.GetRequiredHeight();
        ScrollableCategories.ContentHeight = requiredHeight;

        CategoryVirtualization.ViewportHeight = ScrollableCategories.Size.Y;
        UpdateCategoryVisibility();
    }

    private void OnScrollValueChanged(int scrollPosition)
    {
        CategoryVirtualization.ScrollPosition = scrollPosition;
    }

    private void UpdateCategoryVisibility()
    {
        var nodes = CategoriesNode.Nodes;
        CategoryVirtualization.SetItemCount(nodes.Count);

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is InventoryCategoryNodeBase cat)
            {
                CategoryVirtualization.SetItemLayout(i, cat.Y, cat.Height);
            }
        }

        CategoryVirtualization.UpdateVisibility();
    }

    protected void ResizeWindow(float width, float height, bool recalcLayout)
    {
        SetWindowSize(width, height);

        if (BackgroundDropTarget != null)
        {
            BackgroundDropTarget.Size = ContentSize;
        }

        UpdateHeaderLayout();
        LayoutContent();

        if (recalcLayout)
            CategoriesNode.RecalculateLayout();

        UpdateScrollParameters();
    }

    protected virtual void UpdateHeaderLayout()
    {
        AtkUnitBase* addon = this;
        if (addon == null) return;

        var header = CalculateHeaderLayout(addon);

        if (SearchInputNode != null)
        {
            SearchInputNode.Position = header.SearchPosition;
            SearchInputNode.Size = header.SearchSize;
        }

        if (SettingsButtonNode != null)
        {
            SettingsButtonNode.Position = new Vector2(header.HeaderWidth - SettingsButtonOffset, header.HeaderY);
        }
    }

    protected void ResizeWindow(float width, float height)
        => ResizeWindow(width, height, recalcLayout: true);

    public void ItemRefresh()
    {
        if (!IsOpen) return;
        if (!IsSetupComplete) return;

        RefreshCategoriesCore(false);
    }

    /// <summary>
    /// Rebuilds the search highlight set. The pattern is resolved once instead of
    /// per item - <see cref="ItemInfo.IsRegexMatch(string)"/> performs a regex cache
    /// lookup on every call, which adds up when this runs over every item on each
    /// refresh.
    /// </summary>
    private void RebuildSearchHighlight(IReadOnlyList<CategorizedInventory> categories, string searchText)
    {
        _searchMatchScratch.Clear();

        var re = RegexCache.GetOrCreate(searchText, compiled: false);
        if (re != null)
        {
            for (int i = 0; i < categories.Count; i++)
            {
                var items = categories[i].Items;

                for (int j = 0; j < items.Count; j++)
                {
                    var item = items[j];
                    if (ExternalCategoryManager.MatchesSearchTag(item.Item.ItemId, searchText) ||
                        item.IsRegexMatch(re))
                        _searchMatchScratch.Add(item.Item.ItemId);
                }
            }
        }

        HighlightState.SetFilter(HighlightSource.Search, _searchMatchScratch);
    }

    private void LogRefreshStats()
    {
        long now = Environment.TickCount64;
        if (now - _lastLogTick > 1000) // Log every second
        {
            Services.Logger.DebugOnly($"[Perf] Last 1s: OnRequestedUpdate={_requestedUpdateCount}, RefreshFromLifecycle={_refreshFromLifecycleCount}");
            _requestedUpdateCount = 0;
            _refreshFromLifecycleCount = 0;
            _lastLogTick = now;
        }
    }


    protected override void OnRequestedUpdate(AtkUnitBase* addon, NumberArrayData** numberArrayData, StringArrayData** stringArrayData)
    {
        base.OnRequestedUpdate(addon, numberArrayData, stringArrayData);

        if (DragDropState.IsDragging) return;

        long now = Environment.TickCount64;
        if (now - _lastRefreshTick < MinRefreshIntervalMs) return;

        _lastRefreshTick     = now;
        _requestedUpdateCount++;

        ExecuteRefresh(autosize: true);
    }


    protected override void OnSetup(AtkUnitBase* addon, Span<AtkValue> atkValueSpan)
    {
        ContextMenu = new ContextMenu();

        // Re-arm "compact once per open" so a fresh window compacts again.
        CategoriesNode?.ResetCompactPackingGate();

        // The opening pass already compacts; don't replay a bulk commit that happened
        // while this window was closed.
        _lastSeenBulkCommitVersion = System.LootedItemsTracker.BulkCommitVersion;

        System.AetherBagsAPI?.API.RaiseInventoryOpened();

        if (ScrollableCategories != null)
        {
            ScrollableCategories.ScrollBarNode.OnValueChanged = OnScrollValueChanged;
        }

        base.OnSetup(addon, atkValueSpan);
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        if (RefreshQueued)
        {
            bool doAutosize = RefreshAutosizeQueued;
            RefreshQueued = false;
            RefreshAutosizeQueued = false;

            RefreshCategoriesCore(doAutosize);
        }

        base.OnUpdate(addon);
    }

    private static bool IsFocusedAddon(AtkUnitBase* addon)
    {
        var focusedAddonCount = RaptureAtkUnitManager.Instance()->FocusedUnitsList.Count;
        if (focusedAddonCount == 0) return false;

        var focusedAddon = RaptureAtkUnitManager.Instance()->FocusedUnitsList.Entries[focusedAddonCount - 1];
        if (focusedAddon.Value == null || focusedAddon.Value->Id == 0) return false;

        return focusedAddon.Value->Id == addon->Id || focusedAddon.Value->ParentId == addon->Id;
    }

    protected override void OnFinalize(AtkUnitBase* addon)
    {
        // Reverse P/Invoke boundary: an exception here takes the process down, and
        // during a plugin reload the IPC provider may already be torn down.
        try
        {
            System.AetherBagsAPI?.API.RaiseInventoryClosed();
        }
        catch (Exception ex)
        {
            Services.Logger.Error(ex, "[Inventory] Failed to raise the inventory closed event");
        }

        HoverSubscribed.Clear();
        SearchDebouncer.Dispose();
        RefreshQueued = false;
        RefreshAutosizeQueued = false;
        _deferredPopulationInProgress = false;
        _initialPopulationComplete = false;

        SharedItemNodePool.Clear();
        SharedCategoryNodePool.Clear();
        CategoryVirtualization.ClearLayout();

        base.OnFinalize(addon);
    }
}