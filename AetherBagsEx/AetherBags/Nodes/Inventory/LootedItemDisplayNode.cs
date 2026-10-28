using System;
using AetherBags.Configuration;
using AetherBags.Inventory.Items;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node.Simple;

namespace AetherBags.Nodes.Inventory;

/// <summary>
/// A display-only item node for looted items. Not draggable, but shows tooltip and can be dismissed.
/// </summary>
public sealed unsafe class LootedItemDisplayNode : SimpleComponentNode
{
    private readonly IconNode _iconNode;
    private readonly TextNode _quantityTextNode;

    public Action<LootedItemDisplayNode>? OnDismiss { get; set; }
    public Action<LootedItemDisplayNode>? OnRollOver { get; set; }
    public Action<LootedItemDisplayNode>? OnRollOut { get; set; }

    public LootedItemDisplayNode()
    {
        var style = StylePresets.Current;
        Size = new Vector2(style.ItemWidth, style.ItemHeight);

        _iconNode = new IconNode
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(style.ItemWidth, style.ItemHeight),
        };
        _iconNode.CollisionNode.NodeFlags = 0;
        _iconNode.AttachNode(this);

        CollisionNode.AddEvent(AtkEventType.MouseClick, OnMouseClick);
        CollisionNode.AddEvent(AtkEventType.MouseOver, OnMouseOver);
        CollisionNode.AddEvent(AtkEventType.MouseOut, OnMouseOut);

        _quantityTextNode = new TextNode
        {
            Size = new Vector2(40.0f, 12.0f),
            Position = new Vector2(2.0f, 34.0f),
            TextColor = ColorHelper.GetColor(50),
            TextOutlineColor = ColorHelper.GetColor(51),
            TextFlags = TextFlags.Edge,
            AlignmentType = AlignmentType.Right,
        };
        _quantityTextNode.AttachNode(this);

        ApplySlotMetrics();
    }

    /// <summary>
    /// Resizes the node to the active style's slot metrics and repositions the
    /// quantity label. Called again on reuse so a style switch is picked up even
    /// by nodes that outlive the window.
    /// </summary>
    public void ApplySlotMetrics()
    {
        var style = StylePresets.Current;
        var slotSize = new Vector2(style.ItemWidth, style.ItemHeight);

        // Vector2 has implicit conversions on both sides here, compare components.
        if (MathF.Abs(Size.X - slotSize.X) > 0.01f || MathF.Abs(Size.Y - slotSize.Y) > 0.01f)
            Size = slotSize;

        _iconNode.Size = slotSize;

        _quantityTextNode.Size = new Vector2(MathF.Max(0.0f, slotSize.X - 4.0f), 12.0f);
        _quantityTextNode.Position = new Vector2(2.0f, MathF.Max(0.0f, slotSize.Y - 14.0f));
    }

    public LootedItemInfo? LootedItem
    {
        get;
        set
        {
            bool needsCollisionUpdate = field is null && value is not null;
            field = value;

            if (value is not null)
            {
                InventoryItem item = value.Item;
                _iconNode.IconId = item.IconId;
                _iconNode.ItemTooltip = item.ItemId;
                _iconNode.IsGlamoured = item.Flags.HasFlag(InventoryItem.ItemFlags.Collectable);
                _quantityTextNode.String = value.Quantity > 1 ? value.Quantity.ToString() : string.Empty;
                _iconNode.IsVisible = true;
                _quantityTextNode.IsVisible = true;
            }
            else
            {
                _iconNode.IsVisible = false;
                _quantityTextNode.String = string.Empty;
            }

            if (needsCollisionUpdate)
            {
                var addon = RaptureAtkUnitManager.Instance()->GetAddonByNode(this);
                if (addon is not null)
                    addon->UpdateCollisionNodeList(false);
            }
        }
    } = null;

    private void OnMouseClick(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        if (!atkEventData->IsLeftClick) return;
        OnDismiss?.Invoke(this);
    }

    private void OnMouseOver(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        OnRollOver?.Invoke(this);
    }

    private void OnMouseOut(AtkEventListener* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        OnRollOut?.Invoke(this);
    }

    public void ResetForReuse()
    {
        LootedItem = null;
        _iconNode.IsVisible = false;
        _iconNode.IsGlamoured = false;
        _quantityTextNode.String = string.Empty;
    }
}
