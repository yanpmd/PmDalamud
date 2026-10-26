namespace Una.Drawing;

/// <summary>
/// Defines the properties that specify the presentation of a node.
/// </summary>
public partial class Style
{
    /// <summary>
    /// Defines the color representation of text contents.
    /// </summary>
    public Color? Color { get; set; }

    /// <summary>
    /// Defines the outline color of text contents.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property does not have an effect if <see cref="OutlineSize"/> is
    /// left at 0.
    /// </para>
    /// </remarks>
    public Color? OutlineColor { get; set; }

    /// <summary>
    /// Defines the outline thickness of text contents.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property has no effect if <see cref="OutlineColor"/> is left
    /// undefined.
    /// </para>
    /// </remarks>
    public int? OutlineSize { get; set; }

    /// <summary>
    /// Defines which font to use when rendering text content. This property
    /// references a font by its ID which has been registered using the
    /// <see cref="FontRegistry"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Modifying this property will trigger a reflow of the layout. This is a
    /// computationally expensive operation and should be done sparingly.
    /// </para>
    /// </remarks>
    public uint? Font { get; set; }

    /// <summary>
    /// Specifies the size of the font used to render text contents.
    /// </summary>
    public int? FontSize { get; set; }

    /// <summary>
    /// Defines the line height of multi-line text. This value is a multiplier
    /// of the height of the font.
    /// </summary>
    public float? LineHeight { get; set; }

    /// <summary>
    /// Specifies the alignment of text contents within the bounds of the node.
    /// Defaults to <see cref="Anchor.TopLeft"/>.
    /// </summary>
    public Anchor? TextAlign { get; set; }

    /// <summary>
    /// Defines the offset of the text content from the top-left corner of the
    /// inner bounds of the node.
    /// </summary>
    /// <remarks>
    /// This property does not affect the automatically calculated size of the
    /// node, but is purely a visual offset that can be used if some fonts do
    /// not align correctly.
    /// </remarks>
    public Vector2? TextOffset { get; set; }

    /// <summary>
    /// Whether text is allowed to overflow the bounds of the node. When set to
    /// <c>false</c>, text will be truncated and an ellipsis will be appended to
    /// the end of the text. If <see cref="WordWrap"/> is enabled, text will be
    /// split into multiple lines instead. Defaults to <c>true</c> for best
    /// performance.
    /// </summary>
    public bool? TextOverflow { get; set; }

    /// <summary>
    /// Defines a maximum width for nodes containing text. If the text exceeds
    /// the given width, it will be truncated and an ellipsis will be appended
    /// to the end of the text. Giving this property a value that is not NULL
    /// and higher than 0 will implicitly disable <see cref="TextOverflow"/>
    /// and <see cref="WordWrap"/>.
    /// </summary>
    public float? MaxWidth { get; set; }

    /// <summary>
    /// <para>
    /// Whether to wrap text contents to the next line when they exceed the
    /// defined width of the node.
    /// </para>
    /// <para>
    /// If WordWrap is set to false and the text would exceed the width of the
    /// element, it will be truncated and an ellipsis will be appended to the
    /// end of the text.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Modifying this property will trigger a reflow of the layout. This is a
    /// computationally expensive operation and should be done sparingly.
    /// </remarks>
    public bool? WordWrap { get; set; }

    /// <summary>
    /// Specifies the blur radius of the shadow cast by the text content.
    /// </summary>
    public float? TextShadowSize { get; set; }

    /// <summary>
    /// Defines the color of the shadow cast by the text content.
    /// </summary>
    public Color? TextShadowColor { get; set; }
}