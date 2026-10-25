using System.Linq;
using System.Text;

namespace Una.Drawing;

internal class QuerySelector
{
    /// <summary>
    /// An identifier that must match the current node.
    /// </summary>
    public string? Identifier;

    /// <summary>
    /// True if this selector part must match all nodes.
    /// </summary>
    public bool MatchAll = false;
    
    /// <summary>
    /// A list of class-names that the current node must have.
    /// </summary>
    public readonly List<string> ClassList = [];

    /// <summary>
    /// Represents the tag names that the current node must have.
    /// </summary>
    public readonly List<string> TagList = [];

    /// <summary>
    /// Represents a nested query selector that matches one or more of the
    /// immediate children of the current node.
    /// </summary>
    public QuerySelector? DirectChild;

    /// <summary>
    /// Represents a nested query selector that matches all nested children of
    /// the current node recursively, until it finds one.
    /// </summary>
    public QuerySelector? NestedChild;

    /// <summary>
    /// The parent query selector of the current node.
    /// </summary>
    public QuerySelector? Parent;

    public override string ToString()
    {
        // Selectors are immutable once parsed, and this is called for every
        // matching rule on every style computation.
        return _stringValue ??= BuildString();
    }

    private string? _stringValue;

    private string BuildString()
    {
        StringBuilder sb = new();

        if (Identifier is not null) sb.Append($"#{Identifier}");
        if (ClassList.Count > 0) sb.Append($".{string.Join('.', ClassList)}");
        if (TagList.Count > 0) sb.Append($":{string.Join(':', TagList)}");

        string self = sb.ToString();

        if (Parent is null) {
            return self;
        }

        return Parent.DirectChild == this 
            ? $"{Parent} > {self}" 
            : $"{Parent} {self}";
    }

    /// <summary>
    /// Returns true if the given node matches this query selector part AND
    /// satisfies the ancestor/parent constraints defined by the selector chain.
    /// Assumes this QuerySelector instance is the "leaf" part of the selector being checked.
    /// </summary>
    /// <param name="node">The node to check against this selector part and its ancestors.</param>
    /// <returns>True if the node matches the full selector chain ending at this part.</returns>
    public bool Matches(Node node)
    {
        lock (node.CachedQuerySelectorResults) {

            if (node.CachedQuerySelectorResults.TryGetValue(this, out bool result)) return result;

            // 1. Check if the current node's properties match the current selector part's requirements.
            if (!MatchAll && Identifier != null && !Identifier.Equals(node.Id)) {
                node.CachedQuerySelectorResults.Add(this, false);
                return false;
            }

            // Check Classes (all must be present)
            if (!MatchAll && !ClassList.All(node.ClassList.Contains)) {
                node.CachedQuerySelectorResults.Add(this, false);
                return false;
            }

            // Check Tags (all must be present)
            if (!MatchAll && !TagList.All(node.TagsList.Contains)) {
                node.CachedQuerySelectorResults.Add(this, false);
                return false;
            }

            // 2. If the current node matches this part, check the parent/ancestor constraints.
            if (Parent == null) {
                node.CachedQuerySelectorResults.Add(this, true);
                return true;
            }

            // 3. Determine the relationship expected between the node and its parent/ancestor in the DOM tree.
            // This depends on how *this* selector instance was linked from its Parent selector instance.
            bool isDirectChildLink = Parent.DirectChild == this;
            bool isNestedChildLink = Parent.NestedChild == this;

            if (!isDirectChildLink && !isNestedChildLink) {
                // This should never happen.
                throw new InvalidOperationException(
                    "QuerySelector structure inconsistency: Parent is set, but this instance is neither its DirectChild nor NestedChild.");
            }

            // 4. Apply the hierarchical check based on the link type.
            if (isDirectChildLink) // Represents the '>' child combinator
            {
                // The node's direct parent in the DOM must match the Parent selector.
                if (node.ParentNode == null) {
                    node.CachedQuerySelectorResults.Add(this, false);
                    return false; // Node has no parent, cannot satisfy child combinator.
                }

                // Recursively call Matches on the Parent selector, checking against the node's actual parent.
                bool res = Parent.Matches(node.ParentNode);
                node.CachedQuerySelectorResults.Add(this, res);
                return res;
            }

            // Any ancestor in the DOM must match the Parent selector.
            // Iterate upwards through the node's ancestors.
            Node? ancestor = node.ParentNode;
            while (ancestor != null) {
                // Check if this ancestor satisfies the requirements of the Parent selector (and *its* parents).
                if (Parent.Matches(ancestor)) {
                    node.CachedQuerySelectorResults.Add(this, true);
                    return true; // Found a matching ancestor.
                }

                ancestor = ancestor.ParentNode; // Move up to the next ancestor.
            }

            // If the loop finishes without finding a matching ancestor, the constraint is not met.
            node.CachedQuerySelectorResults.Add(this, false);
        }

        return false;
    }

    private QuerySelector RootNode()
    {
        QuerySelector root = this;

        while (root.Parent != null) {
            root = root.Parent;
        }

        return root;
    }
}