namespace Una.Drawing;

/// <summary>
/// Defines the size of each of the four sides of a rectangle.
/// </summary>
public readonly record struct EdgeSize(float Top, float Right, float Bottom, float Left)
{
    /// <summary>
    /// Returns the combined horizontal size of the left and right edges.
    /// </summary>
    public float HorizontalSize => Left + Right;

    /// <summary>
    /// Returns the combined vertical size of the top and bottom edges.
    /// </summary>
    public float VerticalSize => Top + Bottom;

    /// <summary>
    /// Returns a new Size object with the combined horizontal and vertical
    /// sizes of the defined edges.
    /// </summary>
    public Size Size => new(HorizontalSize, VerticalSize);

    /// <summary>
    /// Defines a uniform perimeter size for all four edges that is zero.
    /// </summary>
    public EdgeSize() : this(0, 0, 0, 0) { }

    /// <summary>
    /// Defines a uniform perimeter size for all four edges.
    /// </summary>
    /// <param name="size">The edge size in pixels.</param>
    public EdgeSize(float size) : this(size, size, size, size) { }

    /// <summary>
    /// Defines the sizes for the vertical and horizontal edges.
    /// </summary>
    /// <param name="vertical">The size of vertical edges in pixels.</param>
    /// <param name="horizontal">The size of horizontal edges in pixels.</param>
    public EdgeSize(float vertical, float horizontal) : this(vertical, horizontal, vertical, horizontal) { }

    public bool IsZero => Top == 0 && Right == 0 && Bottom == 0 && Left == 0;

    public override string ToString() => $"EdgeSize({Top}, {Right}, {Bottom}, {Left})";

    public static bool operator ==(EdgeSize? left, EdgeSize? right) => left is not null && left.Equals(right);
    public static bool operator !=(EdgeSize? left, EdgeSize? right) => !(left == right);

    public static EdgeSize operator *(EdgeSize left, float right) => new(
        (int)Math.Ceiling(left.Top * right),
        (int)Math.Ceiling(left.Right * right),
        (int)Math.Ceiling(left.Bottom * right),
        (int)Math.Ceiling(left.Left * right)
    );

    public EdgeSize Copy()
    {
        return new(Top, Right, Bottom, Left);
    }

    public Vector2 TopLeft {
        get {
            return new Vector2(Left, Top);
        }
    }
}
