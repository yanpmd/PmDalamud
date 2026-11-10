using System;
using System.Collections.Generic;
using KamiToolKit.Extensions;
using KamiToolKit.Nodes;

namespace AetherBags.Compat;

/// <summary>
/// Compatibility shim for <see cref="KamiToolKit.Nodes.MultiStateButtonNode{T}"/>.
/// <para>
/// KamiToolKit made <c>GetStateText</c> a private, non-overridable method, which removes the ability
/// for plugins to supply localized state labels. This shim restores the overridable hook while keeping
/// the behaviour of the upstream node otherwise identical.
/// </para>
/// </summary>
/// <typeparam name="T">Type of the state values.</typeparam>
public class MultiStateButtonNode<T> : TextButtonNode where T : notnull {
    /// <summary>
    /// Gets or sets the action that is invoked when the button is clicked, with the object of the new state.
    /// </summary>
    public Action<T>? OnStateChanged { get; set; }

    /// <summary>
    /// Gets or sets the list of available states.
    /// </summary>
    public required List<T> States {
        get => field;
        set {
            field = value;
            UpdateDisplay();
        }
    }

    /// <summary>
    /// Gets or set the currently selected state.
    /// </summary>
    public T SelectedState {
        get => States[Math.Clamp(SelectedIndex, 0, States.Count - 1)];
        set {
            var index = States.IndexOf(value);
            SelectedIndex = index >= 0 ? index : 0;
        }
    }

    /// <summary>
    /// Constructs a new <see cref="MultiStateButtonNode{T}"/>.
    /// </summary>
    public MultiStateButtonNode()
        => OnClick = CycleState;

    private void CycleState() {
        if (States.Count is 0) return;

        SelectedIndex = (SelectedIndex + 1) % States.Count;
        OnStateChanged?.Invoke(SelectedState);
    }

    private void UpdateDisplay() {
        if (SelectedIndex < 0) return;
        if (SelectedIndex > States.Count - 1) return;

        String = GetStateText(States[SelectedIndex]);
    }

    /// <summary>
    /// Resolves the label shown for the given state.
    /// </summary>
    /// <param name="state">The state to resolve a label for.</param>
    protected virtual string GetStateText(T state) {
        if (state is Enum enumState) {
            return enumState.Description;
        }

        return state.ToString() ?? "Unable to Parse Type";
    }

    private int SelectedIndex {
        get => field;
        set {
            field = value;
            UpdateDisplay();
        }
    }
}
