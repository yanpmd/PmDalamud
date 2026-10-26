
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Una.Drawing;

public partial class Node
{
    /// <summary>
    /// <para>
    /// Invoked immediate after the bounding boxes of all nodes have been
    /// computed and immediately before the reflow process begins.
    /// </para>
    /// <para>
    /// Use this hook to perform any resize operations on the Bounds of the
    /// node, for example, manually resizing the node with certain constraints.
    /// </para>
    /// <remarks>
    /// This callback is only invoked if the node needs to reflow. This only
    /// happens if any property of this node or any of its dependencies has
    /// been modified that would affect its layout.
    /// </remarks>
    /// </summary>
    public ReflowDelegate? BeforeReflow;

    public delegate bool ReflowDelegate(Node node);

    internal Dictionary<Anchor.AnchorPoint, List<Node>> AnchorToChildNodes { get; private set; } = [];

    internal double ReflowTime { get; private set; } = 0f;
    internal double LayoutTime { get; private set; } = 0f;
    internal double DrawTime   { get; private set; } = 0f;

    private bool      _mustReflow = true;
    private Vector2?  _lastPosition;
    private Stopwatch _metricStopwatch = new();
    private Size      _previousSize    = new(0, 0);

    /// <summary>
    /// Computes the bounding size of this node and all its descendants. This method
    /// is invoked automatically when the node is rendered when a reflow is required.
    /// You can call this method if you need to know the size of the node before it
    /// is being drawn.
    /// </summary>
    /// <remarks>
    /// Only the bounding sizes are computed. The bounding rects are not.
    /// </remarks>
    public void ComputeBoundingSize()
    {
        Layout.ComputeBounds(this);
    }

    public void Reflow(Vector2? position = null)
    {
        if (IsDisposed || !ComputedStyle.IsVisible) return;
        if (!_mustReflow && _lastPosition.Equals(position)) return;

        if (!_mustReflow && null != position && !_lastPosition.Equals(position)) {
            _lastPosition = position;
            Layout.OverridePositionsOf(this, position.Value);
            return;
        }

        _mustReflow = false;

        _metricStopwatch.Restart();
        Layout.ComputeBounds(this);
        ReflowTime = _metricStopwatch.Elapsed.TotalMilliseconds;

        InvokeReflowHook();

        _lastPosition = position;
        _metricStopwatch.Restart();
        Layout.ComputeLayout(this, position ?? new Vector2());

        LayoutTime = _metricStopwatch.Elapsed.TotalMilliseconds;
    }

    private bool InvokeReflowHook()
    {
        if (IsDisposed) return false;

        var changed = false;

        foreach (Node child in _childNodes.ToArray()) {
            bool result         = child.InvokeReflowHook();
            if (result) changed = true;
        }

        if (changed) Layout.ComputeBounds(this);

        return (BeforeReflow?.Invoke(this) ?? false) || changed;
    }

    private void ReassignAnchorNodes()
    {
        Dictionary<Anchor.AnchorPoint, List<Node>> anchorNodes = [];

        try {
            foreach (Node child in _childNodes.ToArray()) {
                if (!anchorNodes.TryGetValue(child.ComputedStyle.Anchor.Point, out var value)) {
                    anchorNodes[child.ComputedStyle.Anchor.Point] = value = [];
                }

                value.Add(child);
            }

            AnchorToChildNodes = anchorNodes;
        } catch (ArgumentException) {
            // This can happen if the child node is added or disposed while
            // the ".ToArray()" method is being called, in which case
            // we'll reflow on the next frame anyway.
        }
    }
}