using KamiToolKit.Classes;

namespace KamiToolKit.BaseTypes;

public unsafe partial class NativeAddon {

    /// <summary>
    /// Disables the close button. Use with caution.
    /// </summary>
    public bool DisableClose {
        get;
        set {
            field = value;
            if (InternalAddon is not null) {
                InternalAddon->ShouldFireCallbackAndHideOrClose = value;
            }
        }
    }

    /// <summary>
    /// Disables closing animation. (But doesn't actually...)
    /// </summary>
    public bool DisableCloseTransition {
        get;
        set {
            field = value;
            if (InternalAddon is not null) {
                InternalAddon->DisableHideTransition = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets whether right-clicking the header should show a context menu, for scaling or resetting this addon.
    /// </summary>
    public bool EnableContextMenu {
        get;
        set {
            field = value;
            if (InternalAddon is not null) {
                InternalAddon->EnableTitleBarContextMenu = value;
            }
        }
    } = true;

    /// <summary>
    /// Gets or sets whether this window should be able to be dragged off-screen.
    /// </summary>
    public bool DisableClamping {
        get;
        set {
            field = value;
            if (InternalAddon is not null) {
                FlagHelper.UpdateFlag(ref InternalAddon->Flags1A3, 1 << 5, value);
            }
        }
    } = true;

    /// <summary>
    /// Gets or sets whether the context menu for this addon should allow changing the scale.
    /// </summary>
    public bool DisableScaleContextOption {
        get;
        set {
            field = value;
            if (InternalAddon is not null) {
                InternalAddon->DisableUserScaling = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this addon should close when esc is pressed with no windows focused.
    /// </summary>
    public bool RespectCloseAll { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this addon may take keyboard or gamepad focus.
    /// </summary>
    /// <remarks>
    /// A bar that only displays information should disable this so that clicking it does not move the
    /// focus away from the game.
    /// </remarks>
    public bool DisableFocusability {
        get;
        set {
            field = value;
            if (InternalAddon is not null) InternalAddon->DisableFocusability = value;
        }
    }

    /// <summary>
    /// Gets or sets whether this addon takes focus the moment it is shown.
    /// </summary>
    public bool DisableFocusOnShow {
        get;
        set {
            field = value;
            if (InternalAddon is not null) InternalAddon->DisableFocusOnShow = value;
        }
    }

    /// <summary>
    /// Gets or sets whether escape may close this addon while it does not have focus.
    /// </summary>
    public bool DisableUnfocusedCloseOnEsc {
        get;
        set {
            field = value;
            if (InternalAddon is not null) InternalAddon->DisableUnfocusedCloseOnEsc = value;
        }
    }

    /// <summary>
    /// Gets or sets whether this addon should ignore AtkUnitBase.GlobalScale.
    /// </summary>
    public bool IgnoreGlobalScale { get; set; } = false;

    private void UpdateFlags() {

        // Disable Native AddonConfig
        InternalAddon->DisableAddonConfig = true;
        InternalAddon->ShouldFireCallbackAndHideOrClose = DisableClose;
        InternalAddon->DisableHideTransition = DisableCloseTransition;
        InternalAddon->EnableTitleBarContextMenu = EnableContextMenu;
        InternalAddon->DisableUserScaling = DisableScaleContextOption;

        InternalAddon->DisableFocusability        = DisableFocusability || IsOverlayAddon;
        InternalAddon->DisableFocusOnShow         = DisableFocusOnShow || IsOverlayAddon;
        InternalAddon->DisableUnfocusedCloseOnEsc = DisableUnfocusedCloseOnEsc;

        FlagHelper.UpdateFlag(ref InternalAddon->Flags1A3, 1 << 5, DisableClamping);

        if (IsOverlayAddon) {
            OpenWindowSoundEffectId = 0;
            InternalAddon->ShowSoundEffectId = 0;
            InternalAddon->DisableFocusability = true;
            InternalAddon->DisableFocusOnShow = true;
            InternalAddon->DisableHideTransition = true;
            InternalAddon->DisableShowHideSoundEffects = true;
            InternalAddon->IgnoreUIDisplayMode = true;

            // Disable Controller Nav
            FlagHelper.UpdateFlag(ref InternalAddon->Flags1A2, 0x2, true);

            // Enable ClickThrough
            FlagHelper.UpdateFlag(ref InternalAddon->Flags1A3, 0x40, true);
        }
    }
}
