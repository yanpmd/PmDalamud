namespace Una.Drawing;

public partial class Node
{
    /// <summary>
    /// Defines the sizes and positions of the content and padding areas of this node.
    /// </summary>
    public NodeBounds Bounds { get; internal set; } = new();

    #region Box Model Properties

    /// <summary>
    /// A read-only property that represents the outer width of this node,
    /// based on the total width of the content and padding areas.
    /// </summary>
    public float OuterWidth => ComputedStyle.IsVisible ? Bounds.MarginSize.Width : 0;

    /// <summary>
    /// A read-only property that represents the outer height of this node,
    /// based on the total height of the content and padding areas.
    /// </summary>
    public float OuterHeight => ComputedStyle.IsVisible ? Bounds.MarginSize.Height : 0;

    /// <summary>
    /// A read-only property that represents the width of this node, that is
    /// made up of the content and padding areas.
    /// </summary>
    public float Width => ComputedStyle.IsVisible ? Bounds.PaddingSize.Width : 0;
    
    /// <summary>
    /// A read-only property that represents the height of this node, that is
    /// made up of the content and padding areas.
    /// </summary>
    public float Height => ComputedStyle.IsVisible ? Bounds.PaddingSize.Height : 0;
    
    /// <summary>
    /// A read-only property that represents the width of this node that is
    /// made up of the content area.
    /// </summary>
    public float InnerWidth => ComputedStyle.IsVisible ? Bounds.ContentSize.Width : 0;

    /// <summary>
    /// A read-only property that represents the height of this node that is
    /// made up of the content area.
    /// </summary>
    public float InnerHeight => ComputedStyle.IsVisible ? Bounds.ContentSize.Height : 0;

    #endregion
}
