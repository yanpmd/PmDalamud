namespace Una.Drawing;

public partial class Style
{
    /// <summary>
    /// <para>
    /// Defines a single background color for the element.
    /// </para>
    /// <para>
    /// If both <see cref="BackgroundColor"/> and <see cref="BackgroundGradient"/>
    /// are defined, the gradient will be rendered on top of the background color.
    /// </para>
    /// </summary>
    public Color? BackgroundColor { get; set; }

    /// <summary>
    /// <para>
    /// Defines a 4-point gradient background for the element.
    /// </para>
    /// <para>
    /// If both <see cref="BackgroundColor"/> and <see cref="BackgroundGradient"/>
    /// are defined, the gradient will be rendered on top of the background color.
    /// </para>
    /// </summary>
    public GradientColor? BackgroundGradient { get; set; }

    /// <summary>
    /// Defines the inset of the gradient background which determines the space
    /// between the gradient background and the element's border.
    /// </summary>
    public EdgeSize? BackgroundGradientInset { get; set; }

    /// <summary>
    /// An image to be displayed in the background of the element. Can be either
    /// a byte array of an image file or an icon ID.
    /// </summary>
    public object? BackgroundImage { get; set; }

    /// <summary>
    /// Defines the inset of the background image which determines the space
    /// between the background image and the element's border.
    /// </summary>
    public EdgeSize? BackgroundImageInset { get; set; }

    /// <summary>
    /// Specifies the scale of the background image. Higher values will make the
    /// image appear more often in the background.
    /// </summary>
    public Vector2? BackgroundImageScale { get; set; }

    /// <summary>
    /// Specifies the color to apply to the background image. Defaults to white.
    /// </summary>
    public Color? BackgroundImageColor { get; set; }

    /// <summary>
    /// Specifies the rotation of the background image in degrees.
    /// </summary>
    public short? BackgroundImageRotation { get; set; }

    /// <summary>
    /// Defines the color blend mode to apply to the background image.
    /// This works in conjunction with <see cref="BackgroundImageColor"/>.
    /// </summary>
    public BlendMode? BackgroundImageBlendMode { get; set; }
}
