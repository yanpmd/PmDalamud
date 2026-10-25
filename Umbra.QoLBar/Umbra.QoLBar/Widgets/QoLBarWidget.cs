using System;
using System.Collections.Generic;
using Dalamud.Interface;
using Umbra.Common;
using Umbra.QoLBar.UI;
using Umbra.Widgets;

namespace Umbra.QoLBar.Widgets;

/// <summary>
/// A toolbar widget that represents a single QoL Bar. Each widget instance is
/// bound to one bar configuration (identified by the "BarId" config variable).
/// </summary>
/// <remarks>
/// Opening the widget renders the bar itself (see <see cref="BarOverlay"/>)
/// instead of a text menu, so categories expand as additional QoLBar-style
/// bars instead of unfolding into a single flattened menu.
/// </remarks>
[ToolbarWidget(
    "QoLBar",
    "QoL Bar",
    "A customizable bar of shortcuts, ported from QoLBarCN. Click to open the bar's shortcuts.",
    ["shortcut", "bar", "command", "macro", "qolbar"]
)]
public class QoLBarWidget(
    WidgetInfo                  info,
    string?                     guid         = null,
    Dictionary<string, object>? configValues = null
) : StandardToolbarWidget(info, guid, configValues)
{
    /// <summary>
    /// This widget shows the bar name as text and a customizable icon.
    /// </summary>
    protected override StandardWidgetFeatures Features =>
        StandardWidgetFeatures.Text |
        StandardWidgetFeatures.Icon |
        StandardWidgetFeatures.CustomizableIcon;

    /// <summary>
    /// The bar is not rendered through Umbra's menu popup system; the overlay
    /// draws it directly so that nested categories can pop out on their own.
    /// </summary>
    public override WidgetPopup? Popup => null;

    private QoLBarManager Manager => Framework.Service<QoLBarManager>();

    private string _barId;

    /// <inheritdoc/>
    protected override void OnLoad()
    {
        _barId = GetConfigValue<string>("BarId");
        if (string.IsNullOrEmpty(_barId)) _barId = Id;

        var config = Manager.Config;

        if (!config.Bars.ContainsKey(_barId))
        {
            // Adopt an orphaned bar (e.g. the demo bar on first start) when available.
            var orphan = Manager.AdoptOrphanedBar();
            if (orphan != null)
            {
                _barId = orphan;
                SetConfigValue("BarId", orphan);
            }
            else
            {
                Manager.GetOrCreateBar(_barId);
            }
        }

        Manager.ClaimBar(_barId);

        // If the user has not picked an icon yet, use the bar's first icon.
        TrySetDefaultIcon();

        Node.OnMouseDown += _ => OnWidgetMouseDown();
    }

    /// <inheritdoc/>
    protected override void OnUnload()
    {
        if (BarOverlay.IsOpenFor(_barId)) BarOverlay.Close();

        Manager.ReleaseBar(_barId);
    }

    /// <inheritdoc/>
    protected override void OnDraw()
    {
        // Runs the standard text/icon layout logic of the base widget.
        base.OnDraw();

        var bar = Manager.GetBar(_barId);
        if (bar == null) return;

        // Hide the widget while the bar's conditions are not met.
        IsVisible = Manager.IsBarVisible(bar);

        if (!IsVisible && BarOverlay.IsOpenFor(_barId)) BarOverlay.Close();

        SetText(string.IsNullOrEmpty(bar.Name) ? Loc.Get("Bar.DefaultName") : bar.Name);

        if (bar.ShortcutList.Count == 0 && Manager.Config.Settings.ShowWidgetHint)
        {
            SetTooltip(Loc.Get("Widget.EmptyBarTooltip"));
        }
        else
        {
            SetTooltip(null);
        }

        // Keep the opened bars aligned with the widget when the toolbar moves.
        if (BarOverlay.IsOpenFor(_barId))
        {
            var bounds = Node.Bounds.MarginRect;
            BarOverlay.UpdateAnchor(_barId, new(bounds.X1, bounds.Y1), new(bounds.X2, bounds.Y2));
        }
    }

    /// <inheritdoc/>
    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables()
    {
        return [
            // Identifies the bar this widget instance is bound to. Copied
            // together with the instance when the user duplicates the widget,
            // which allows showing the same bar in multiple toolbars.
            new StringWidgetConfigVariable(
                "BarId",
                "Bar ID",
                "Internal ID of the bar this widget is bound to.",
                Id
            ) { IsHidden = true },

            ..base.GetConfigVariables(),
        ];
    }

    private void OnWidgetMouseDown()
    {
        if (Node.IsDisabled) return;

        var bounds = Node.Bounds.MarginRect;

        BarOverlay.Toggle(_barId, new(bounds.X1, bounds.Y1), new(bounds.X2, bounds.Y2));
    }

    private void TrySetDefaultIcon()
    {
        var bar = Manager.GetBar(_barId);
        if (bar == null) return;

        if (GetConfigValue<uint>("GameIconId") != 113) return;
        if (GetConfigValue<string>("IconType") != "Game") return;

        foreach (var sh in bar.ShortcutList)
        {
            var name = sh.Name;
            if (!IconManager.ParseName(ref name, out _, out _, out var icon, out _)) continue;
            if (icon <= 0) continue;

            SetConfigValue("GameIconId", (uint)icon);
            return;
        }
    }
}
