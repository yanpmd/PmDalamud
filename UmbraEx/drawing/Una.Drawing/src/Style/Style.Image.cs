using Dalamud.Game.Text.SeStringHandling;

namespace Una.Drawing;

public partial class Style
{
    /// <summary>
    /// Draws a game icon in the node.
    /// </summary>
    /// <remarks>
    /// This value is mutually exclusive with <see cref="BitmapFontIcon"/>
    /// and <see cref="UldResource"/> and its associated properties.
    /// </remarks>
    public uint? IconId { get; set; }

    /// <summary>
    /// A byte array of an image file to be displayed in the node.
    /// This property takes precedence over <see cref="IconId"/>.
    /// </summary>
    public byte[]? ImageBytes { get; set; }

    /// <summary>
    /// Defines the space between the icon and the border edges of the node.
    /// </summary>
    public EdgeSize? ImageInset { get; set; }

    /// <summary>
    /// A translation offset for the icon.
    /// </summary>
    public Vector2? ImageOffset { get; set; }

    /// <summary>
    /// Specifies the rounding of the icon.
    /// </summary>
    public float? ImageRounding { get; set; }

    /// <summary>
    /// Specifies the corners of the icon that should be rounded.
    /// </summary>
    public RoundedCorners? ImageRoundedCorners { get; set; }

    /// <summary>
    /// Whether the icon should be displayed in black and white.
    /// </summary>
    public bool? ImageGrayscale { get; set; }

    /// <summary>
    /// A contrast value for the icon. Must range between 0 and 1.
    /// </summary>
    public float? ImageContrast { get; set; }

    /// <summary>
    /// Defines the rotation of the image in degrees.
    /// </summary>
    public short? ImageRotation { get; set; }

    /// <summary>
    /// Defines the color to apply to the image.
    /// </summary>
    public Color? ImageColor { get; set; }

    /// <summary>
    /// Defines the blend mode to apply to the image. Use this in conjunction
    /// with <see cref="ImageColor"/>.
    /// </summary>
    public BlendMode? ImageBlendMode { get; set; }

    /// <summary>
    /// The scale mode to apply to the image. Defaults to
    /// <see cref="ImageScaleMode.Adapt"/>.
    /// </summary>
    public ImageScaleMode? ImageScaleMode { get; set; }
    
    /// <summary>
    /// Specifies if the image should be clamped or repeated.
    /// </summary>
    public ImageTileMode? ImageTileMode { get; set; }

    /// <summary>
    /// Defines the scale of the image. Defaults to 1.0f.
    /// </summary>
    public float? ImageScale { get; set; }
    
    /// <summary>
    /// Applies a gaussian blur to the image in the X and Y axis.
    /// </summary>
    public Vector2? ImageBlur { get; set; }
    
    /// <summary>
    /// Defines the bitmap font icon to render.
    /// </summary>
    /// <remarks>
    /// This value is mutually exclusive with <see cref="IconId"/> and
    /// <see cref="UldResource"/> and its associated properties.
    /// </remarks>
    public BitmapFontIcon? BitmapFontIcon { get; set; }
    
    /// <summary>
    /// Defines the resource path of the ULD file to be displayed in the node.
    /// </summary>
    /// <remarks>
    /// This value is mutually exclusive with <see cref="IconId"/>
    /// and <see cref="BitmapFontIcon"/>.
    /// </remarks>
    public string? UldResource { get; set; }

    /// <summary>
    /// The ULD style to be displayed in the node.
    /// </summary>
    /// <remarks>
    /// This value is mutually exclusive with <see cref="IconId"/>
    /// and <see cref="BitmapFontIcon"/>.
    /// </remarks>
    public UldStyle? UldStyle { get; set; }

    /// <summary>
    /// Defines the ULD parts id to be displayed in the node.
    /// </summary>
    /// <remarks>
    /// This value is mutually exclusive with <see cref="IconId"/>
    /// and <see cref="BitmapFontIcon"/>.
    /// </remarks>
    public int? UldPartsId { get; set; }

    /// <summary>
    /// Defines the ULD part id to be displayed in the node.
    /// </summary>
    /// <remarks>
    /// This value is mutually exclusive with <see cref="IconId"/>
    /// and <see cref="BitmapFontIcon"/>.
    /// </remarks>
    public int? UldPartId { get; set; }
    
    /// <summary>
    /// Adds a drop shadow to an image.
    /// The elements of the vector are defined as follows:
    /// <list type="bullet">
    ///   <item>X: The X offset of the shadow.</item>
    ///   <item>Y: The Y offset of the shadow.</item>
    ///   <item>Z: The horizontal sigma of the shadow.</item>
    ///   <item>W: The vertical sigma of the shadow.</item>
    /// </list>
    /// </summary>
    public Vector4? DropShadow { get; set; }
}