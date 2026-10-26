using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Linq;
using Vector2 = System.Numerics.Vector2;

namespace Una.Drawing.Clipping;

public static class ClipRectProvider
{
    private static readonly List<ClipRect> RectList = [];

    public static unsafe void UpdateRects()
    {
        RectList.Clear();

        ForEachAtkUnit(
            (uPtr, _) => {
                var unitBase   = (AtkUnitBase*)uPtr;
                var rootNode   = unitBase->RootNode;
                var windowNode = unitBase->WindowNode;

                if (rootNode == null || windowNode == null) return;
                if (!rootNode->IsVisible()) return;

                Vector2 scale = new(rootNode->ScaleX, rootNode->ScaleY);

                ClipRect windowRect = GetWindowClipRect(windowNode, scale);
                if (windowRect.IsValid()) RectList.Add(windowRect);
            }
        );
    }

    private static unsafe ClipRect GetWindowClipRect(AtkComponentNode* componentNode, Vector2 scale)
    {
        var component = componentNode->Component;
        if (component == null) return new ClipRect();

        ClipRect rect = new();
        
        for (var i = 0; i < component->UldManager.NodeListSize; i++) {
            var node = component->UldManager.NodeList[i];
            if (node == null) continue;
            if (!node->IsVisible()) continue;

            Vector2  size     = new Vector2(node->Width, node->Height) * scale;
            Vector2  topLeft  = new(node->ScreenX, node->ScreenY);
            ClipRect clipRect = new ClipRect(topLeft, topLeft + size);

            if (clipRect.IsValid()) {
                rect.X1 = rect.X1 > 0 ? Math.Min(rect.X1, clipRect.X1) : clipRect.X1;
                rect.Y1 = rect.Y1 > 0 ? Math.Min(rect.Y1, clipRect.Y1) : clipRect.Y1;
                rect.X2 = Math.Max(rect.X2, clipRect.X2);
                rect.Y2 = Math.Max(rect.Y2, clipRect.Y2);
            }
        }

        return rect;
    }

    public static List<ClipRect> FindClipRectsIntersectingWith(ClipRect clipRect)
    {
        return RectList.Where(other => other.IntersectsWith(clipRect)).ToList();
    }

    /// <summary>
    /// Returns the AtkUnitManager instance.
    /// </summary>
    /// <returns></returns>
    private static unsafe AtkUnitManager* GetAtkUnitManager()
    {
        AtkStage* stage = AtkStage.Instance();

        if (stage == null) {
            return null;
        }

        RaptureAtkUnitManager* manager = stage->RaptureAtkUnitManager;

        if (manager == null) {
            return null;
        }

        return &manager->AtkUnitManager;
    }

    /// <summary>
    /// Iterates over all visible ATK units and calls the given iterator for each.
    /// </summary>
    /// <param name="iterator"></param>
    /// <param name="focusedOnly"></param>
    private static unsafe void ForEachAtkUnit(Action<IntPtr, string> iterator, bool focusedOnly = false)
    {
        var unitManager = GetAtkUnitManager();
        var unitList    = focusedOnly ? unitManager->FocusedUnitsList : unitManager->AllLoadedUnitsList;

        for (var i = 0; i < unitList.Count; i++) {
            AtkUnitBase* unit = *(AtkUnitBase**)Unsafe.AsPointer(ref unitList.Entries[i]);

            if (null == unit || !unit->IsVisible || null == unit->WindowNode) continue;

            string? name = unit->NameString;
            if (name is "_FocusTargetInfo" or "JobHudNotice") continue;
            
            // Ignore VanillaPlus "OverlayController" which is not visible on screen
            if (name != null && name.StartsWith("KTK_Overlay")) continue;

            iterator((IntPtr)unit, name ?? string.Empty);
        }
    }
}