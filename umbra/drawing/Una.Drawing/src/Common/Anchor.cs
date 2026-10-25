namespace Una.Drawing;

public class Anchor(Anchor.AnchorPoint point)
{
    public readonly AnchorPoint Point = point;

    /// <inheritdoc cref="GetOffset(System.Numerics.Vector2)"/>
    public Vector2 GetOffset(Size size)
    {
        return GetOffset(new Vector2(size.Width, size.Height));
    }

    /// <summary>
    /// Returns the offset position for the given size based on the configured
    /// anchor point.
    /// </summary>
    /// <param name="size">The size to calculate an offset for.</param>
    /// <returns>The calculated offset vector.</returns>
    public Vector2 GetOffset(Vector2 size)
    {
        return Point switch {
            AnchorPoint.None         => Vector2.Zero,
            AnchorPoint.TopLeft      => new(0, 0),
            AnchorPoint.TopCenter    => new(-size.X / 2, 0),
            AnchorPoint.TopRight     => new(-size.X, 0),
            AnchorPoint.MiddleLeft   => new(0, -size.Y / 2),
            AnchorPoint.MiddleCenter => new(-size.X / 2, -size.Y / 2),
            AnchorPoint.MiddleRight  => new(-size.X, -size.Y / 2),
            AnchorPoint.BottomLeft   => new(0, -size.Y),
            AnchorPoint.BottomCenter => new(-size.X / 2, -size.Y),
            AnchorPoint.BottomRight  => new(-size.X, -size.Y),
            _                        => new()
        };
    }

    /// <summary>
    /// True if the anchor point is set to <see cref="AnchorPoint.None"/>. In
    /// this case, the offset is zero and the node will not be anchored to any
    /// neighboring nodes.
    /// </summary>
    public bool IsNone => Point == AnchorPoint.None;

    /// <summary>
    /// True if the vertical anchor point is set to the top.
    /// </summary>
    public bool IsTop => Point is AnchorPoint.TopLeft or AnchorPoint.TopCenter or AnchorPoint.TopRight;

    /// <summary>
    /// True if the vertical anchor point is in the middle.
    /// </summary>
    public bool IsMiddle => Point is AnchorPoint.MiddleLeft or AnchorPoint.MiddleCenter or AnchorPoint.MiddleRight;

    /// <summary>
    /// True if the vertical anchor point is set to the bottom.
    /// </summary>
    public bool IsBottom => Point is AnchorPoint.BottomLeft or AnchorPoint.BottomCenter or AnchorPoint.BottomRight;

    /// <summary>
    /// True if the horizontal anchor point is set to the left.
    /// </summary>
    public bool IsLeft => Point is AnchorPoint.TopLeft or AnchorPoint.MiddleLeft or AnchorPoint.BottomLeft;

    /// <summary>
    /// True if the horizontal anchor point is in the center.
    /// </summary>
    public bool IsCenter => Point is AnchorPoint.TopCenter or AnchorPoint.MiddleCenter or AnchorPoint.BottomCenter;

    /// <summary>
    /// True if the horizontal anchor point is set to the right.
    /// </summary>
    public bool IsRight => Point is AnchorPoint.TopRight or AnchorPoint.MiddleRight or AnchorPoint.BottomRight;

    public static Anchor None         => new(AnchorPoint.None);
    public static Anchor TopLeft      => new(AnchorPoint.TopLeft);
    public static Anchor TopCenter    => new(AnchorPoint.TopCenter);
    public static Anchor TopRight     => new(AnchorPoint.TopRight);
    public static Anchor MiddleLeft   => new(AnchorPoint.MiddleLeft);
    public static Anchor MiddleCenter => new(AnchorPoint.MiddleCenter);
    public static Anchor MiddleRight  => new(AnchorPoint.MiddleRight);
    public static Anchor BottomLeft   => new(AnchorPoint.BottomLeft);
    public static Anchor BottomCenter => new(AnchorPoint.BottomCenter);
    public static Anchor BottomRight  => new(AnchorPoint.BottomRight);

    public static implicit operator Anchor(AnchorPoint point) => new(point);

    public static bool operator ==(Anchor      left, Anchor      right) => left.Point == right.Point;
    public static bool operator !=(Anchor      left, Anchor      right) => left.Point != right.Point;
    public static bool operator ==(Anchor      left, AnchorPoint right) => left.Point == right;
    public static bool operator !=(Anchor      left, AnchorPoint right) => left.Point != right;
    public static bool operator ==(AnchorPoint left, Anchor      right) => left == right.Point;
    public static bool operator !=(AnchorPoint left, Anchor      right) => left != right.Point;

    public override bool Equals(object? obj) =>
        (obj is Anchor a && a.Point == Point) || (obj is AnchorPoint p && p == Point);

    public override int GetHashCode() => Point.GetHashCode();

    public enum AnchorPoint : byte
    {
        None,
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    public override string ToString()
    {
        return Point.ToString();
    }
}