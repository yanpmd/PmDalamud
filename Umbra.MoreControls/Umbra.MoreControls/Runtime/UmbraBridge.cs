using System.Reflection;
using Umbra.Widgets;

namespace Umbra.MoreControls.Runtime;

/// <summary>
/// Reflection bridge into Umbra internals.
///
/// Umbra's <c>WidgetManager</c> and <c>Toolbar</c> types are <c>internal</c>, so
/// a plugin cannot reference them directly. Everything used here is a
/// <c>public</c> member of an internal type (or a private field that Umbra's own
/// partial class exposes), which keeps this layer small and easy to keep in
/// sync with upstream.
/// </summary>
internal static class UmbraBridge
{
    private const string WidgetManagerTypeName = "Umbra.Widgets.System.WidgetManager";
    private const string ToolbarTypeName       = "Umbra.Toolbar";

    private static readonly Assembly UmbraAssembly = typeof(ToolbarWidget).Assembly;

    private static bool _resolved;
    private static bool _reportedFailure;

    private static object?       _widgetManager;
    private static MethodInfo?   _getWidgetInstances;
    private static MethodInfo?   _createWidget;
    private static PropertyInfo? _isVisibleProperty;

    private static object?     _toolbar;
    private static MethodInfo? _getPanel;
    private static FieldInfo?  _toolbarNodeField;

    /// <summary>
    /// Widget instances currently on the toolbar. Empty when Umbra's internals
    /// could not be resolved (e.g. after an Umbra update changed them).
    /// </summary>
    public static List<ToolbarWidget> GetWidgets()
    {
        if (!EnsureResolved()) return [];
        if (_widgetManager is null || _getWidgetInstances is null) return [];

        try {
            if (_getWidgetInstances.Invoke(_widgetManager, null) is not System.Collections.IEnumerable list) return [];

            return list.OfType<ToolbarWidget>().ToList();
        } catch (Exception e) {
            ReportFailure(e);
            return [];
        }
    }

    /// <summary>
    /// Applies a visibility state to a widget instance. Both the protected
    /// <c>IsVisible</c> property (which Umbra re-reads every tick) and the node
    /// style (which takes effect on the current frame) are updated.
    /// </summary>
    public static void SetWidgetVisible(ToolbarWidget widget, bool visible)
    {
        try {
            _isVisibleProperty?.SetValue(widget, visible);
            Node? node = widget.Node;

            if (node is not null) node.Style.IsVisible = visible;
        } catch (Exception e) {
            ReportFailure(e);
        }
    }

    /// <summary>
    /// Returns the toolbar's root node ("Toolbar") or one of its panels
    /// ("Left", "Center", "Right").
    /// </summary>
    public static Node? GetPanelNode(string id)
    {
        if (!EnsureResolved() || _toolbar is null) return null;

        try {
            if (id == "Toolbar") {
                return _toolbarNodeField?.GetValue(_toolbar) as Node;
            }

            return _getPanel?.Invoke(_toolbar, [id]) as Node;
        } catch (Exception e) {
            ReportFailure(e);
            return null;
        }
    }

    /// <summary>
    /// Adds a widget instance to the toolbar. Used once on a fresh install so
    /// that the plugin has a visible entry point.
    /// </summary>
    public static bool CreateWidget(string widgetId, string location)
    {
        if (!EnsureResolved() || _widgetManager is null || _createWidget is null) return false;

        try {
            _createWidget.Invoke(_widgetManager, [widgetId, location, null, null, null, true]);

            return true;
        } catch (Exception e) {
            ReportFailure(e);
            return false;
        }
    }

    public static void SetPanelVisible(string id, bool visible)
    {
        try {
            Node? node = GetPanelNode(id);

            if (node is not null) node.Style.IsVisible = visible;
        } catch (Exception e) {
            ReportFailure(e);
        }
    }

    public static bool IsResolved => _resolved;

    private static bool EnsureResolved()
    {
        if (_resolved) return true;

        try {
            Type? widgetManagerType = UmbraAssembly.GetType(WidgetManagerTypeName);
            Type? toolbarType       = UmbraAssembly.GetType(ToolbarTypeName);

            if (widgetManagerType is null || toolbarType is null) {
                ReportFailure(new InvalidOperationException("Umbra internals not found."));
                return false;
            }

            _widgetManager       = Framework.Service<object>(widgetManagerType);
            _getWidgetInstances  = widgetManagerType.GetMethod("GetWidgetInstances", BindingFlags.Public | BindingFlags.Instance);
            _createWidget        = widgetManagerType.GetMethod("CreateWidget", BindingFlags.Public | BindingFlags.Instance);
            _isVisibleProperty   = typeof(ToolbarWidget).GetProperty("IsVisible", BindingFlags.NonPublic | BindingFlags.Instance);

            _toolbar          = Framework.Service<object>(toolbarType);
            _getPanel         = toolbarType.GetMethod("GetPanel", BindingFlags.Public | BindingFlags.Instance);
            _toolbarNodeField = toolbarType.GetField("_toolbarNode", BindingFlags.NonPublic | BindingFlags.Instance);

            _resolved = _widgetManager is not null && _toolbar is not null && _getWidgetInstances is not null;

            if (_resolved) {
                Logger.Info("[MoreControls] Umbra internals resolved.");
            } else {
                ReportFailure(new InvalidOperationException("Umbra services could not be resolved."));
            }
        } catch (Exception e) {
            ReportFailure(e);
        }

        return _resolved;
    }

    private static void ReportFailure(Exception e)
    {
        if (_reportedFailure) return;
        _reportedFailure = true;

        Logger.Warning($"[MoreControls] Failed to access Umbra internals: {e.GetType().Name}: {e.Message}");
    }
}
