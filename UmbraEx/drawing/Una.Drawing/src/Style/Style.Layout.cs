namespace Una.Drawing;

/// <summary>
/// Defines the properties that specify the presentation of an element.
/// </summary>
public partial class Style
{
    /// <summary>
    /// Determines whether this node is visible.
    /// </summary>
    public bool? IsVisible { get; set; }

    /// <summary>
    /// <para>
    /// The anchor defines the point of origin of the element.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Modifying this property will trigger a reflow of the layout. This is a
    /// computationally expensive operation and should be done sparingly.
    /// </remarks>
    public Anchor? Anchor { get; set; }

    /// <summary>
    /// Defines the flow direction of the node.
    /// </summary>
    public Flow? Flow { get; set; }
    
    /// <summary>
    /// Defines the render order of child elements.
    /// </summary>
    public FlowOrder? FlowOrder { get; set; }

    /// <summary>
    /// Defines the gap between nodes.
    /// </summary>
    public float? Gap { get; set; }
}
