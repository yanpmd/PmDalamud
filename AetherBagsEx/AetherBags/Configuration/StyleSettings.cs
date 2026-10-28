using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AetherBags.Configuration;

/// <summary>
/// Persisted style selection. The concrete values live in <see cref="StylePresets"/>;
/// only the chosen preset is stored so new presets can be added without migration.
/// </summary>
public class StyleSettings
{
    public InventoryStylePreset Preset { get; set; } = InventoryStylePreset.Default;
}

public enum InventoryStylePreset : byte
{
    Default = 0,
    Compact = 1,
    Comfortable = 2,
    Classic = 3,
    Minimal = 4,
    HighContrast = 5,
}

/// <summary>
/// How categories are arranged inside the bag window.
/// </summary>
public enum CategoryLayoutMode : byte
{
    /// <summary>Categories keep their natural width and wrap onto the next row.</summary>
    Wrapping = 0,

    /// <summary>Every category spans the full content width, one category per row.</summary>
    SingleColumn = 1,
}

/// <summary>
/// The resolved set of visual + layout parameters that make up one "style".
/// Every value that used to be hardcoded is funnelled through here so a preset
/// can restyle the whole window (chrome, spacing and item metrics) at once.
/// </summary>
public sealed class InventoryStyle
{
    #region Window chrome

    /// <summary>Nine grid stretch insets of the window background/border (acts as the "corner" size).</summary>
    public Vector4 WindowOffsets { get; init; }

    /// <summary>Additive tint applied to the window background.</summary>
    public Vector3 WindowTint { get; init; }

    /// <summary>Opacity of the window background, 0 - 1.</summary>
    public float WindowAlpha { get; init; }

    /// <summary>Shows the WindowA gradation overlay behind the content.</summary>
    public bool ShowWindowGradation { get; init; }

    /// <summary>Shows the divider line underneath the window title.</summary>
    public bool ShowDividingLine { get; init; }

    public FontType TitleFontType { get; init; }
    public uint TitleFontSize { get; init; }
    public uint TitleColorId { get; init; }
    public uint TitleOutlineColorId { get; init; }

    #endregion

    #region Layout

    public CategoryLayoutMode LayoutMode { get; init; }

    /// <summary>Gap between category blocks, both horizontally and vertically.</summary>
    public float CategorySpacing { get; init; }

    /// <summary>Height reserved for a category's name label.</summary>
    public float CategoryHeaderHeight { get; init; }

    public uint CategoryHeaderFontSize { get; init; }

    public float ItemWidth { get; init; }
    public float ItemHeight { get; init; }

    /// <summary>Gap between item slots inside a category.</summary>
    public float ItemPadding { get; init; }

    public float FooterHeight { get; init; }

    #endregion
}
