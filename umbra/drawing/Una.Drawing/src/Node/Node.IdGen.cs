using Lumina.Misc;

namespace Una.Drawing;

public partial class Node
{
    private string? _internalId;
    private uint?   _internalIdCrc32;
    private Node?   _internalIdLastParent;
    private int?    _internalIdLastIndex;

    /// <summary>
    /// Returns the internal ID of this node.
    /// </summary>
    public string InternalId
    {
        get
        {
            InvalidateInternalId();

            return _internalId ??= GenerateInternalId();
        }
    }

    /// <summary>
    /// Returns the CRC32 hash of the internal ID of this node.
    /// </summary>
    private uint InternalIdCrc32
    {
        get
        {
            InvalidateInternalId();

            return _internalIdCrc32 ??= Crc32.Get(InternalId);
        }
    }

    /// <summary>
    /// Invalidates the internal id of this node in case the parent
    /// or index has changed.
    /// </summary>
    private void InvalidateInternalId()
    {
        if (IsDisposed) return;

        if (ParentNode != _internalIdLastParent || ChildNodes.IndexOf(this) != _internalIdLastIndex) {
            _internalIdLastParent = ParentNode;
            _internalIdLastIndex  = ChildNodes.IndexOf(this);
            _internalId           = null;
            _internalIdCrc32      = null;
        }
    }

    /// <summary>
    /// Generates the internal ID of this node based on this and its parent nodes.
    /// </summary>
    private string GenerateInternalId()
    {
        if (IsDisposed) return string.Empty;

        List<string> breadcrumbs = [GetInternalIdOfThis()];

        Node? current = ParentNode;
        while (current != null) {
            breadcrumbs.Insert(0, current.GetInternalIdOfThis());
            current = current.ParentNode;
        }

        return string.Join(">", breadcrumbs);
    }

    /// <summary>
    /// Generates an internal id chunk based on this node only.
    /// </summary>
    /// <returns></returns>
    private string GetInternalIdOfThis()
    {
        // Get the node index of this node in the parent's child nodes list.
        int    nodeIndex  = ParentNode?.ChildNodes.IndexOf(this) ?? 0;
        var    internalId = $"{Id ?? ""}#{nodeIndex}";
        string classList  = string.Join(".", ClassList);
        string tagsList   = string.Join(":", TagsList);

        return $"Umbra:{internalId}[{classList}]({tagsList})";
    }
}