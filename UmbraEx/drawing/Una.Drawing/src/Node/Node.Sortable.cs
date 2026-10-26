using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

using System.Collections.Immutable;
using System.Linq;
using Task = System.Threading.Tasks.Task;

namespace Una.Drawing;

public partial class Node
{
    /// <summary>
    /// Invoked when the user has sorted child nodes after dragging them around.
    /// </summary>
    public event Action<Node>? OnSorted;
    
    /// <summary>
    /// <para>
    /// Enables or disables sorting behavior for this node.
    /// </para>
    /// <para>
    /// This automatically makes all immediate children interactive and allows
    /// the user to sort the child nodes by dragging them around. The flow
    /// direction of the node will be used to determine the sorting direction.
    /// </para>
    /// </summary>
    public bool Sortable {
        get => _isSortingEnabled;
        set {
            _isSortingEnabled = value;

            if (value) {
                EnableSorting();
            } else {
                DisableSorting();
            }
        }
    }

    /// <summary>
    /// Specifies a group identifier for sortable nodes. This is used to
    /// allow dragging and dropping between different sortable nodes. For
    /// example, if you have two sortable nodes with the same group ID, you
    /// can drag a child node from one to the other.
    /// </summary>
    public string? SortableGroupId { get; set; }

    private bool  _isSortingEnabled;
    private Node? _sortableHoveredNode;
    private Node? _sortableDraggedNode;
    private int   _sortableDraggedNodeIndex;
    private Node? _trackedDroppableNode;

    private void EnableSorting()
    {
        OnChildAdded   += OnSortableChildAdded;
        OnChildRemoved += OnSortableChildRemoved;

        foreach (Node child in _childNodes.ToArray()) {
            OnSortableChildAdded(child);
        }
    }

    private void DisableSorting()
    {
        OnChildAdded   -= OnSortableChildAdded;
        OnChildRemoved -= OnSortableChildRemoved;

        foreach (Node child in _childNodes.ToArray()) {
            OnSortableChildRemoved(child);
        }
    }

    private void OnSortableChildAdded(Node child)
    {
        // Every child node must have a valid SortIndex.
        if (child.SortIndex == -1) {
            child.SortIndex = _childNodes.Count - 1; // SortIndex is 0-based.
        }

        child.OnDragStart += OnSortableChildDragStart;
        child.OnDragMove  += OnSortableChildDragMove;
        child.OnDragEnd   += OnSortableChildDragEnd;
    }

    private void OnSortableChildRemoved(Node child)
    {
        // Re-apply sort indices on all children to fill the gap.
        for (int i = 0; i < _childNodes.Count; i++) {
            _childNodes[i].SortIndex = i;
        }

        child.OnDragStart -= OnSortableChildDragStart;
        child.OnDragMove  -= OnSortableChildDragMove;
        child.OnDragEnd   -= OnSortableChildDragEnd;
    }

    private void CheckDroppableNode()
    {
        if (!Sortable || null == SortableGroupId || null == DraggedNode) return; // Not a droppable container.
        if (SortableGroupId != DraggedNode.ParentNode?.SortableGroupId) return;  // Not the same group.
        if (DraggedNode.ParentNode == this) return;                              // Already in this node.

        if (!IsMouseInNodeBounds(this)) {
            ToggleTag("droppable", false);
            UntrackDroppableNode();
            return;
        }

        ToggleTag("droppable", true);
        TrackDroppableNode();
    }

    private void TrackDroppableNode()
    {
        if (_trackedDroppableNode == DraggedNode) return;

        // Safety check.
        if ((DraggedNode == null && _trackedDroppableNode != null) || DraggedNode != _trackedDroppableNode) {
            UntrackDroppableNode();
        }

        if (DraggedNode == null) return;

        _sortableDraggedNode              =  DraggedNode;
        _trackedDroppableNode             =  DraggedNode;
        _trackedDroppableNode.OnDragStart += OnSortableChildDragStart;
        _trackedDroppableNode.OnDragMove  += OnSortableChildDragMove;
        _trackedDroppableNode.OnDragEnd   += OnSortableChildDragEnd;
    }

    private void UntrackDroppableNode()
    {
        if (_trackedDroppableNode == null) return;

        _trackedDroppableNode.OnDragStart -= OnSortableChildDragStart;
        _trackedDroppableNode.OnDragMove  -= OnSortableChildDragMove;
        _trackedDroppableNode.OnDragEnd   -= OnSortableChildDragEnd;
        _trackedDroppableNode             =  null;
        _sortableDraggedNode              =  null;
    }

    private void OnSortableChildDragStart(Node child)
    {
        _sortableDraggedNode = child;
    }

    private void OnSortableChildDragMove(Node child)
    {
        _sortableDraggedNode = child;
        
        if (!IsMouseInNodeBounds(this, new(), false)) {
            UntrackDroppableNode();
            return;
        }
        
        TrackDroppableNode();

        _sortableHoveredNode = FindNodeAtMousePos();
        
        if (_sortableHoveredNode == child) return;
        
        if (null == _sortableHoveredNode && ChildNodes.LastOrDefault()?.Bounds.MarginRect.Center.Y + ScrollY < ImGui.GetMousePos().Y) {
            _sortableHoveredNode = ChildNodes.LastOrDefault();
        }
        
        if (_sortableHoveredNode == null || _sortableHoveredNode == child) return;
        
        Vector2 startPos;
        Vector2 endPos;
        bool    isAbove;

        if (ComputedStyle.Flow == Flow.Vertical) {
            isAbove = ImGui.GetMousePos().Y < _sortableHoveredNode.Bounds.MarginRect.Center.Y + ScrollY;
            startPos = isAbove
                ? _sortableHoveredNode.Bounds.MarginRect.TopLeft
                : _sortableHoveredNode.Bounds.MarginRect.BottomLeft;
            endPos = isAbove
                ? _sortableHoveredNode.Bounds.MarginRect.TopRight
                : _sortableHoveredNode.Bounds.MarginRect.BottomRight;

            // If we have a gap between the nodes, we need to adjust the start and end positions.
            if (ComputedStyle.Gap > 0) {
                startPos.Y += (isAbove ? -ComputedStyle.Gap : ComputedStyle.Gap) / 2f;
                endPos.Y   =  startPos.Y;
            }
        } else {
            isAbove = ImGui.GetMousePos().X < _sortableHoveredNode.Bounds.MarginRect.Center.X + ScrollX;

            startPos = isAbove
                ? _sortableHoveredNode.Bounds.MarginRect.TopLeft
                : _sortableHoveredNode.Bounds.MarginRect.TopRight;
            endPos = isAbove
                ? _sortableHoveredNode.Bounds.MarginRect.BottomLeft
                : _sortableHoveredNode.Bounds.MarginRect.BottomRight;

            // If we have a gap between the nodes, we need to adjust the start and end positions.
            if (ComputedStyle.Gap > 0) {
                startPos.X += (isAbove ? -ComputedStyle.Gap : ComputedStyle.Gap) / 2f;
                endPos.X   =  startPos.X;
            }
        }

        // Draw a line between the two nodes to indicate the swap.
        ImGui.GetForegroundDrawList().AddLine(
            startPos, endPos,
            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 1)), // TODO: Make color configurable.
            2f
        );

        // Find the new sort index for the dragged node.
        _sortableDraggedNodeIndex = _sortableHoveredNode.SortIndex + (isAbove ? 0 : 1);
    }

    private void OnSortableChildDragEnd(Node child)
    {
        ToggleTag("droppable", false);

        StabilizeSortIndices();
        
        if (_sortableDraggedNode == null) {
            _sortableDraggedNode      = null;
            _sortableHoveredNode      = null;
            _sortableDraggedNodeIndex = -1;
            return;
        }

        if (_trackedDroppableNode != null && _trackedDroppableNode.ParentNode != this) {
            _trackedDroppableNode.SortIndex = _sortableHoveredNode?.SortIndex ?? _sortableDraggedNodeIndex;
            _sortableDraggedNode            = _trackedDroppableNode;
            AppendChild(_trackedDroppableNode);
            StabilizeSortIndices();
        }

        // Run delayed to allow the Node itself to update based on SortIndex changes.
        DalamudServices.Framework.RunOnTick(() => {
            UpdateSortedChildIndices();
            StabilizeSortIndices();
            RaiseEvent(OnSorted);
        });
    }
    
    private bool IsMouseInNodeBounds(Node node, EdgeSize expand = default, bool useScrollOffset = true)
    {
        Rect rect = node.Bounds.MarginRect.Copy();
        rect.Expand(expand);

        return rect.Contains(ImGui.GetMousePos() - (useScrollOffset ? GetScrollPos() : Vector2.Zero));
    }

    private Node? FindNodeAtMousePos()
    {
        float g  = ComputedStyle.Gap;
        float ph = ComputedStyle.Padding.HorizontalSize;
        float pv = ComputedStyle.Padding.VerticalSize;

        EdgeSize expand = ComputedStyle.Flow == Flow.Vertical ? new(g, ph) : new(pv, g);

        lock (_childNodes) {
            foreach (Node node in _childNodes) {
                if (_sortableDraggedNode != null && node == _sortableDraggedNode) continue;
                if (IsMouseInNodeBounds(node, expand, false)) return node;
            }
        }

        return null;
    }

    private Vector2 GetScrollPos()
    {
        var node = this;

        while (node != null) {
            if (node.Overflow == false) {
                return new Vector2(node.ScrollX, node.ScrollY);
            }

            node = node.ParentNode;
        }

        return new Vector2(0, 0);
    }

    /// <summary>
    /// Updates the sort indices of the child nodes based on the current
    /// dropped node.
    /// </summary>
    /// <remarks>
    /// This method runs on the framework thread and should therefore not
    /// invoke any ImGui or other rendering methods.
    /// </remarks>
    private void UpdateSortedChildIndices()
    {
        if (_sortableDraggedNode == null) return;
        
        if (_sortableHoveredNode == null) {
            _sortableHoveredNode           = ChildNodes.LastOrDefault();
            _sortableDraggedNode.SortIndex = _sortableHoveredNode?.SortIndex + 1 ?? 0;
        }

        if (_sortableDraggedNodeIndex == _sortableDraggedNode.SortIndex) return;

        if (_sortableHoveredNode == null) {
            StabilizeSortIndices();
            return;
        }
        
        int  calculatedTargetSlot = _sortableDraggedNodeIndex;
        Node hoveredNode          = _sortableHoveredNode;
        Node draggedNode          = _sortableDraggedNode;

        lock (_childNodes) {
            int draggedIndex = _childNodes.IndexOf(draggedNode);
            if (draggedIndex == -1) return;

            int finalTargetIndex = calculatedTargetSlot;

            bool droppedOnTopHalf = (calculatedTargetSlot == hoveredNode.SortIndex);
            if (droppedOnTopHalf && hoveredNode.SortIndex == draggedIndex + 1) {
                finalTargetIndex = draggedIndex;
            }

            bool droppedOnBottomHalf = (calculatedTargetSlot == hoveredNode.SortIndex + 1);
            if (droppedOnBottomHalf && hoveredNode.SortIndex == draggedIndex - 1) {
                finalTargetIndex = draggedIndex;
            }

            if (finalTargetIndex == calculatedTargetSlot && droppedOnBottomHalf &&
                draggedIndex < hoveredNode.SortIndex) {
                finalTargetIndex = hoveredNode.SortIndex;
            }

            int targetIndex = finalTargetIndex;

            if (draggedIndex == targetIndex) {
                _sortableDraggedNode      = null;
                _sortableHoveredNode      = null;
                _sortableDraggedNodeIndex = -1;
                StabilizeSortIndices();
                return;
            }

            Dictionary<Node, int> newSortIndex = [];

            for (int i = 0; i < _childNodes.Count; i++) {
                Node currentNode = _childNodes[i];

                if (currentNode == draggedNode) {
                    newSortIndex[currentNode] = targetIndex;
                    continue;
                }

                if (draggedIndex < targetIndex) {
                    newSortIndex[currentNode] = i > draggedIndex && i <= targetIndex ? i - 1 : i;
                    continue;
                }

                if (i >= targetIndex && i < draggedIndex) {
                    newSortIndex[currentNode] = i + 1;
                    continue;
                }

                newSortIndex[currentNode] = i;
            }

            foreach (var (node, index) in newSortIndex) {
                if (index == node.SortIndex) continue;
                node.SortIndex = index;
            }
        }
        
        _sortableDraggedNode      = null;
        _sortableHoveredNode      = null;
        _sortableDraggedNodeIndex = -1;
        StabilizeSortIndices();
    }

    private void StabilizeSortIndices()
    {
        lock (_childNodes) {
            Dictionary<Node, int> newSortIndex = [];

            for (var i = 0; i < _childNodes.Count; i++) {
                Node node = _childNodes[i];
                if (node.SortIndex != i) newSortIndex[node] = i;
            }

            // Apply the new sort indices outside the original iteration, since
            // the original list updates when sort index is changed.
            foreach (var (node, index) in newSortIndex) {
                node.SortIndex = index;
            }
        }
    }
}