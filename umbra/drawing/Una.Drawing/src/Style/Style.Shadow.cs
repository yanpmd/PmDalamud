namespace Una.Drawing;

public partial class Style
{
    /// <summary>
    /// Defines the shadow size on the node for each side.
    /// </summary>
    public EdgeSize? ShadowSize { get; set; }

    /// <summary>
    /// Defines the inset of the shadow on the node in pixels.
    /// </summary>
    public int? ShadowInset { get; set; }

    /// <summary>
    /// Defines the offset of the shadow on the node in pixels.
    /// </summary>
    public Vector2? ShadowOffset { get; set; }
}
