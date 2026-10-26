namespace Una.Drawing;

/// <summary>
/// Defines the properties that specify the presentation of an element.
/// </summary>
public partial class Style
{
    /// <summary>
    /// <para>
    /// Padding is inserted between the content edge and the padding edge of an
    /// element, providing spacing between the content and its border.
    /// </para>
    /// <para>
    /// If the padding is 0, the content will touch the border edge of the
    /// element. If the padding is negative, the content will overflow the
    /// border edge of the element, allowing the content to be rendered outside
    /// the bounding box of the element.
    /// </para>
    /// <para>
    /// If the padding is larger than the definitive size of the element that
    /// has been set by the <see cref="Style.Size"/> property, the definitive
    /// size will be floored to 0, meaning that content and children will not
    /// be rendered.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Modifying this property will trigger a reflow of the layout. This is a
    /// computationally expensive operation and should be done sparingly.
    /// </remarks>
    public EdgeSize? Padding { get; set; }

    /// <summary>
    /// Margins are inserted between the margin edge and the padding edge of an
    /// element, providing spacing between the element and its parent node.
    /// </summary>
    /// <remarks>
    /// Modifying this property will trigger a reflow of the layout. This is a
    /// computationally expensive operation and should be done sparingly.
    /// </remarks>
    public EdgeSize? Margin { get; set; }
    
    /// <summary>
    /// <para>
    /// Specifies the definitive inner dimensions of the content area of an
    /// element, including the padding edges.
    /// </para>
    /// <para>
    /// The size of an element must be large enough to fit the padding edge
    /// size. If the size is too small, the padding will overflow the content
    /// size will be floored to 0, meaning that content and children will not
    /// be rendered.
    /// </para>
    /// <para>
    /// A size of 0 on either axis will let the element automatically adjust
    /// its size to fit its content and child nodes.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Modifying this property will trigger a reflow of the layout. This is a
    /// computationally expensive operation and should be done sparingly.
    /// </remarks>
    public Size? Size { get; set; }
    
    /// <summary>
    /// <para>
    /// Specifies the behavior of automatic sizing across the horizontal and
    /// vertical axes. If an axis is set to <see cref="AutoSize.Fit"/> or
    /// <see cref="AutoSize.Grow"/>, the Size property will be foreced to 0
    /// on that axis.
    /// </para>
    /// <para>
    /// If the <see cref="Style.Size"/> is set to 0 on either axis, the
    /// behavior defaults to <see cref="AutoSize.Fit"/>. This means that the
    /// node will automatically adjust its size to fit its content and child
    /// nodes.
    /// </para>
    /// <para>
    /// When an axis is set to <see cref="AutoSize.Grow"/>, the node will
    /// expand to fill the available space in the parent node. If more than
    /// one child node is set to grow, the available space will be divided
    /// equally among them.
    /// </para>
    /// </summary>
    public (AutoSize Horizontal, AutoSize Vertical)? AutoSize { get; set; }
}
