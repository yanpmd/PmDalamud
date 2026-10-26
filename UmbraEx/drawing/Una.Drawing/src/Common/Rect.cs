namespace Una.Drawing;

public class Rect(float x1, float y1, float x2, float y2) : IEquatable<Rect>
{
    public float X1 { get; set; } = x1;
    public float Y1 { get; set; } = y1;
    public float X2 { get; set; } = x2;
    public float Y2 { get; set; } = y2;

    public Vector2 TopLeft     => new(X1, Y1);
    public Vector2 TopRight    => new(X2, Y1);
    public Vector2 BottomLeft  => new(X1, Y2);
    public Vector2 BottomRight => new(X2, Y2);
    public Vector2 Center => new((X1 + X2) / 2, (Y1 + Y2) / 2);
    
    public float Width  => X2 - X1;
    public float Height => Y2 - Y1;

    public bool IsEmpty => X1 == 0 && Y1 == 0 && X2 == 0 && Y2 == 0;

    public Rect(float     x,        float     y, Size size) : this(x, y, x + size.Width, y + size.Height) { }
    public Rect(Vector2 position, Size    size) : this((int)position.X, (int)position.Y, size) { }
    public Rect(Vector2 position, Vector2 size) : this((int)position.X, (int)position.Y, (int)size.X, (int)size.Y) { }
    public Rect(Size    size) : this(0, 0, size) { }

    /// <summary>
    /// Creates and returns a copy of this rect.
    /// </summary>
    /// <returns></returns>
    public Rect Copy()
    {
        return new(X1, Y1, X2, Y2);
    }

    /// <summary>
    /// Expands this rectangle by the given edge size.
    /// </summary>
    /// <param name="edge"></param>
    public void Expand(EdgeSize edge)
    {
        X1 -= edge.Left;
        Y1 -= edge.Top;
        X2 += edge.Right;
        Y2 += edge.Bottom;
    }

    /// <summary>
    /// Shrinks this rectangle by the given edge size.
    /// </summary>
    /// <param name="edge"></param>
    public void Shrink(EdgeSize edge)
    {
        X1 += edge.Left;
        Y1 += edge.Top;
        X2 -= edge.Right;
        Y2 -= edge.Bottom;
    }

    /// <summary>
    /// Returns true if the given pofloat is contained within this rectangle.
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public bool Contains(Vector2 point) => point.X >= X1 && point.X <= X2 && point.Y >= Y1 && point.Y <= Y2;

    /// <summary>
    /// Returns true if the given rectangle is contained within this rectangle.
    /// </summary>
    public bool Contains(Rect rect) => rect.X1 >= X1 && rect.X2 <= X2 && rect.Y1 >= Y1 && rect.Y2 <= Y2;

    /// <summary>
    /// Returns true if this rectangle entirely overlaps the given rectangle.
    /// </summary>
    public bool Overlaps(Rect rect) => X1 < rect.X1 && X2 > rect.X2 && Y1 < rect.Y1 && Y2 > rect.Y2;

    /// <summary>
    /// Returns true if this rectangle intersects with another.
    /// </summary>
    public bool Intersects(Rect rect) => X1 < rect.X2 && X2 > rect.X1 && Y1 < rect.Y2 && Y2 > rect.Y1;

    /// <summary>
    /// Returns a new rectangle that represents the intersection of this
    /// rectangle with another.
    /// </summary>
    /// <param name="rect">The other rectangle.</param>
    /// <returns>The intersection between this rectangle and the other one.</returns>
    public Rect Intersection(Rect rect) =>
        new(MathF.Max(X1, rect.X1), MathF.Max(Y1, rect.Y1), MathF.Min(X2, rect.X2), MathF.Min(Y2, rect.Y2));

    /// <summary>
    /// Returns a new rectangle that represents the union of this rectangle
    /// and another.
    /// </summary>
    /// <param name="rect">The rectangle to form a union with.</param>
    /// <returns>The union of this and the other rectangle.</returns>
    public Rect Union(Rect rect) =>
        new(MathF.Min(X1, rect.X1), MathF.Min(Y1, rect.Y1), MathF.Max(X2, rect.X2), MathF.Max(Y2, rect.Y2));

    public static implicit operator SKRect(Rect rect) => new(rect.X1, rect.Y1, rect.X2, rect.Y2);
    public static implicit operator Rect(SKRect rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    public override string ToString()
    {
        return $"({X1}x{Y1} - {X2}x{Y2})";
    }

    public bool Equals(Rect? other)
    {
        return (
            Math.Abs((other?.X1 ?? 0) - X1) < 0.1f && 
            Math.Abs((other?.Y1 ?? 0) - Y1) < 0.1f && 
            Math.Abs((other?.X2 ?? 0) - X2) < 0.1f && 
            Math.Abs((other?.Y2 ?? 0) - Y2) < 0.1f
        );
    }
}
