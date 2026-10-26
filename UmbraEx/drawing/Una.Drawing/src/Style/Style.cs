using System.Linq;
using System.Text;

namespace Una.Drawing;

/// <summary>
/// Defines the properties that specify the presentation of an element.
/// </summary>
public sealed partial class Style
{
    /// <summary>
    /// The amount of time, in milliseconds, that a transition from one
    /// style to another should take. Use the <see cref="TransitionType"/>
    /// property to configure the easing function used for the transition.
    /// </summary>
    public uint? TransitionDuration { get; set; }
    
    /// <summary>
    /// The type of transition to use when changing from one style to another.
    /// </summary>
    public TransitionType? TransitionType { get; set; }
    
    /// <summary>
    /// A class name to add to the node when the current transition is done.
    /// This effectively allows you to ping-pong between two classes to keep
    /// animations going.
    /// </summary>
    public string? TransitionAddClass { get; set; }
    
    /// <summary>
    /// Removes the given class from this node when the transition is done.
    /// </summary>
    public string? TransitionRemoveClass { get; set; }
    
    /// <summary>
    /// Specifies the opacity of the node. Must be a value between 0 and 1.
    /// </summary>
    public float? Opacity { get; set; }

    /// <summary>
    /// Whether the node's texture should be antialiased. This applies to
    /// background images, icons, and borders. Text is always antialiased,
    /// regardless of this setting.
    /// </summary>
    public bool? IsAntialiased { get; set; }

    public override string ToString()
    {
        Type type = GetType();
        
        var properties = type.GetProperties()
            .Where(p => p is { CanRead: true, CanWrite: true } && p.GetValue(this) != null)
            .Select(p => $"    {p.Name}: {p.GetValue(this)}")
            .ToList();

        StringBuilder sb = new();
        
        foreach (var property in properties) {
            sb.AppendLine(property);
        }
        
        return sb.ToString();
    }
}
