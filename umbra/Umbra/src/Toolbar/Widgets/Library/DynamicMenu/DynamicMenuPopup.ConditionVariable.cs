using Umbra.Conditions;
using Umbra.Windows.Library.VariableEditor;

namespace Umbra.Widgets;

internal sealed partial class DynamicMenuPopup
{
    /// <summary>
    /// Creates the "visibility condition" control that is shown in the
    /// configuration window of every menu entry.
    /// </summary>
    private static StringSelectVariable CreateConditionVariable(DynamicMenuEntry entry)
    {
        return new("ConditionSetId") {
            Name        = I18N.Translate("Widget.DynamicMenu.ConditionEditor.Label.Name"),
            Description = I18N.Translate("Widget.DynamicMenu.ConditionEditor.Label.Description"),
            Choices     = Framework.Service<ConditionSetManager>().GetChoices(),
            Value       = entry.Cn ?? string.Empty,
        };
    }

    /// <summary>
    /// Writes the value of the control created by
    /// <see cref="CreateConditionVariable"/> back to the entry.
    /// </summary>
    private static void ApplyConditionVariable(DynamicMenuEntry entry, StringSelectVariable variable)
    {
        entry.Cn = string.IsNullOrWhiteSpace(variable.Value) ? null : variable.Value;
    }
}
