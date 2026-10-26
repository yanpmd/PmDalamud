

namespace Una.Drawing.Clipping;

public struct ClipRect(Vector2 topLeft, Vector2 bottomRight) : IEquatable<ClipRect>
{
    public float X1 = topLeft.X;
    public float Y1 = topLeft.Y;
    public float X2 = bottomRight.X;
    public float Y2 = bottomRight.Y;

    public ClipRect(float x1, float y1, float x2, float y2)
        : this(new Vector2(x1, y1), new Vector2(x2, y2)) { }

    public static ClipRect FromRect(Rect rect) => new(rect.TopLeft, rect.BottomRight);

    public readonly float   Width       => MathF.Max(0, X2 - X1);
    public readonly float   Height      => MathF.Max(0, Y2 - Y1);
    public readonly float   Area        => Width * Height;
    public readonly Vector2 TopLeft     => new(X1, Y1);
    public readonly Vector2 BottomRight => new(X2, Y2);

    /// <summary>
    /// Checks if the rectangle has a valid, non-negative area
    /// </summary>
    public readonly bool IsValid(float epsilon = 1e-4f) => (X2 - X1) > epsilon && (Y2 - Y1) > epsilon;

    /// <summary>
    /// Checks whether this rectangle intersects with another rectangle,
    /// including edges.
    /// </summary>
    public readonly bool IntersectsWith(ClipRect other) =>
        X1 < other.X2 && X2 > other.X1 &&
        Y1 < other.Y2 && Y2 > other.Y1;

    /// <summary>
    /// Check whether this rectangle is completely contained within another rectangle.
    /// </summary>
    public readonly bool Overlaps(ClipRect other) =>
        X1 <= other.X1 && Y1 <= other.Y1 &&
        X2 >= other.X2 && Y2 >= other.Y2;

    
    /// <summary>
    /// Returns a new ClipRect that is clamped to the given bounds.
    /// </summary>
    public readonly ClipRect Clamp(ClipRect bounds) =>
        new(
            MathF.Max(X1, bounds.X1), MathF.Max(Y1, bounds.Y1),
            MathF.Min(X2, bounds.X2), MathF.Min(Y2, bounds.Y2)
        );

    /// <summary>
    /// Returns 0 to 4 rectangles representing the parts of 'this' rectangle
    /// that are not covered by 'other' rectangle.
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public readonly List<ClipRect> Subtract(ClipRect other)
    {
        List<ClipRect> result = [];

        // If no intersection, 'this' remains whole.
        if (!IntersectsWith(other)) {
            result.Add(this);
            return result;
        }

        // Clamp 'other' to the bounds of 'this' to find the actual intersection area
        float intersectX1 = MathF.Max(X1, other.X1);
        float intersectY1 = MathF.Max(Y1, other.Y1);
        float intersectX2 = MathF.Min(X2, other.X2);
        float intersectY2 = MathF.Min(Y2, other.Y2);

        // If 'other' completely covers 'this', result is empty.
        // Use a small epsilon for float comparison robustness
        const float epsilon = 1e-4f;
        if (intersectX1 <= X1 + epsilon && intersectY1 <= Y1 + epsilon &&
            intersectX2 >= X2 - epsilon && intersectY2 >= Y2 - epsilon) {
            return result; // Fully occluded
        }

        // Generate up to 4 rectangles representing the parts of 'this' *not* covered by 'other'

        // Top rectangle
        if (intersectY1 > Y1) {
            var topRect = new ClipRect(X1, Y1, X2, intersectY1);
            if (topRect.IsValid(epsilon)) result.Add(topRect);
        }

        // Bottom rectangle
        if (intersectY2 < Y2) {
            var bottomRect = new ClipRect(X1, intersectY2, X2, Y2);
            if (bottomRect.IsValid(epsilon)) result.Add(bottomRect);
        }

        // Left rectangle (within the vertical bounds of the intersection)
        if (intersectX1 > X1) {
            var leftRect = new ClipRect(X1, intersectY1, intersectX1, intersectY2);
            if (leftRect.IsValid(epsilon)) result.Add(leftRect);
        }

        // Right rectangle (within the vertical bounds of the intersection)
        if (intersectX2 < X2) {
            var rightRect = new ClipRect(intersectX2, intersectY1, X2, intersectY2);
            if (rightRect.IsValid(epsilon)) result.Add(rightRect);
        }

        return result;
    }

    /// <summary>
    /// Returns true if the given point is contained within this rectangle,
    /// including edges.
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public bool Contains(Vector2 pos) =>
        pos.X >= X1 && pos.X <= X2 &&
        pos.Y >= Y1 && pos.Y <= Y2;

    /// <summary>
    /// Returns the top-left coordinate of this rectangle.
    /// </summary>
    public Vector2 Min => new(X1, Y1);
    
    /// <summary>
    /// Returns the bottom-right coordinate of this rectangle.
    /// </summary>
    public Vector2 Max => new(X2, Y2);

    public override readonly bool Equals(object? obj) => obj is ClipRect rect && Equals(rect);
    public override readonly int GetHashCode() => HashCode.Combine(X1, Y1, X2, Y2);
    
    public readonly bool Equals(ClipRect other) => X1 == other.X1 && Y1 == other.Y1 && X2 == other.X2 && Y2 == other.Y2;
    public static bool operator ==(ClipRect left, ClipRect right) => left.Equals(right);
    public static bool operator !=(ClipRect left, ClipRect right) => !(left == right);

    /// <summary>
    /// Renders this rectangle to the foreground draw list. Used for
    /// debugging purposes only.
    /// </summary>
    /// <param name="color"></param>
    public void RenderToScreen(uint color = 0xFF0000FF)
    {
        ImDrawListPtr drawList = ImGui.GetForegroundDrawList();
        drawList.AddRect(new(X1, Y1), new(X2, Y2), color);
    }
}