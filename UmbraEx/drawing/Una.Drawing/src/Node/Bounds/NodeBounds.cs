namespace Una.Drawing;

public class NodeBounds
{
    /// <summary>
    /// Represents the size of the margin area of this node, which is the area
    /// that surrounds the content and padding areas of the node.
    /// </summary>
    public Size MarginSize { get; set; } = new(0, 0);
    
    /// <summary>
    /// Represents the size of the border area of this node, which is the area
    /// that surrounds the content and padding areas of the node.
    /// </summary>
    public Size PaddingSize { get; set; } = new(0, 0);

    /// <summary>
    /// Represents the size of the content area of this node, which is the area
    /// that contains the bounding box areas of its children or the content of
    /// the node itself, whichever is larger.
    /// </summary>
    public Size ContentSize  { get; set; } = new(0, 0);

    /// <summary>
    /// Represents the margin area of this node, which is the area that surrounds
    /// the content and padding areas of the node.
    /// </summary>
    public Rect MarginRect { get; internal set; } = new(0, 0, 0, 0);
    
    /// <summary>
    /// Represents the border area of this node, which is the area that surrounds
    /// the content and padding areas of the node.
    /// </summary>
    public Rect PaddingRect { get; internal set; } = new(0, 0, 0, 0);

    /// <summary>
    /// Represents the content area of this node, which is the area that contains
    /// the bounding box areas of its children or the content of the node itself,
    /// whichever is larger.
    /// </summary>
    public Rect ContentRect { get; internal set; } = new(0, 0, 0, 0);
}
