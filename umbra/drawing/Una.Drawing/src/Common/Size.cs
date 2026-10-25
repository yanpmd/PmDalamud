namespace Una.Drawing;

/// <summary>
/// Defines the size of an object in pixels. A size of 0 on either axis is
/// considered undefined, meaning that the object will be sized based on its
/// content and child nodes.
/// </summary>
public record Size(float Width = 0, float Height = 0)
{
    public float Width  { get; set; } = Width;
    public float Height { get; set; } = Height;

    public Size() : this(0, 0) { }
    public Size(float size) : this(size, size) { }

    /// <summary>
    /// True if both sides have an undefined size, meaning that the box should
    /// be sized both horizontally and vertically based on its content and
    /// child nodes.
    /// </summary>
    public bool IsAuto => Width == 0 && Height == 0;

    /// <summary>
    /// True if both sides have a defined size, meaning that the box has a
    /// definitive size and should not be auto-sized based on its content and
    /// child nodes.
    /// </summary>
    public bool IsFixed => Width > 0 && Height > 0;

    /// <summary>
    /// True if the width is 0, meaning that the box should only be auto-sized
    /// horizontally based on its content and child nodes.
    /// </summary>
    public bool IsFitWidth => Width == 0;

    /// <summary>
    /// True if the height is undefined, meaning that the box should only be
    /// auto-sized vertically based on its content and child nodes.
    /// </summary>
    public bool IsFitHeight => Height == 0;

    /// <summary>
    /// True if the size is 0 on both axes, meaning that the box should be
    /// invisible and not take up any space.
    /// </summary>
    public bool IsZero => Width == 0 && Height == 0;

    public override string ToString()    => $"({Width}, {Height})";

    public static Size operator +(Size  left, Size right) => new(left.Width + right.Width, left.Height + right.Height);
    public static Size operator -(Size  left, Size right) => new(left.Width - right.Width, left.Height - right.Height);
    public static Size operator *(Size left, float right) => new((int)Math.Ceiling(left.Width * right), (int)Math.Ceiling(left.Height * right));
    public static Size operator /(Size left, float right) => new((int)Math.Floor(left.Width / right), (int)Math.Floor(left.Height / right));

    /// <summary>
    /// Creates a copy of this size object.
    /// </summary>
    /// <returns></returns>
    public Size Copy()
    {
        return new(Width, Height);
    }

    /// <summary>
    /// Returns a new <see cref="Vector2"/> object with the same width and
    /// height as this size object.
    /// </summary>
    /// <returns></returns>
    public Vector2 ToVector2()
    {
        return new(Width, Height);
    }
}
