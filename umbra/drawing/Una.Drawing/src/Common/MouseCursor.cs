using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Una.Drawing;

internal static unsafe class MouseCursor
{
    private const uint ArrowType     = (uint)AtkCursor.CursorType.Arrow;
    private const uint ClickableType = (uint)AtkCursor.CursorType.Clickable;

    private static readonly HashSet<Node> MouseOverNodes = [];

    private static bool CursorIsChangedByUs;

    internal static void RegisterMouseOver(Node node)
    {
        MouseOverNodes.Add(node);
    }

    internal static void RemoveMouseOver(Node node)
    {
        MouseOverNodes.Remove(node);
    }

    internal static void Dispose()
    {
        MouseOverNodes.Clear();
    }

    internal static void Update()
    {
        Cursor* cursor = Framework.Instance()->Cursor;
        if (cursor == null) return;

        var hoverCount = MouseOverNodes.Count;

        if (MouseOverNodes.Count > 0 && cursor->ActiveCursorType != ClickableType)
        {
            cursor->ActiveCursorType = ClickableType;
            CursorIsChangedByUs = true;
        }
        else if (hoverCount == 0 && cursor->ActiveCursorType == ClickableType && CursorIsChangedByUs)
        {
            cursor->ActiveCursorType = ArrowType;
            CursorIsChangedByUs = false;
        }

        MouseOverNodes.Clear();
    }
}