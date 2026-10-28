using System;
using System.Numerics;
using AetherBags.Helpers;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AetherBags.Configuration;

/// <summary>
/// Registry of the built-in styles. Presets only use assets that ship with the
/// game's WindowA texture family plus parameters that are safe to tweak at
/// runtime (insets, tint, alpha, fonts, sizes and spacing).
/// </summary>
public static class StylePresets
{
    /// <summary>Available presets mapped to their localization key.</summary>
    public static readonly (InventoryStylePreset preset, string nameKey)[] Items =
    [
        (InventoryStylePreset.Default, "Style.Default"),
        (InventoryStylePreset.Compact, "Style.Compact"),
        (InventoryStylePreset.Comfortable, "Style.Comfortable"),
        (InventoryStylePreset.Classic, "Style.Classic"),
        (InventoryStylePreset.Minimal, "Style.Minimal"),
        (InventoryStylePreset.HighContrast, "Style.HighContrast"),
    ];

    /// <summary>The style matching the configured preset.</summary>
    public static InventoryStyle Current => Resolve(GetConfiguredPreset());

    public static InventoryStylePreset GetConfiguredPreset()
        => System.Config?.General?.Style?.Preset ?? InventoryStylePreset.Default;

    public static InventoryStyle Resolve(InventoryStylePreset preset) => preset switch
    {
        InventoryStylePreset.Compact => Compact,
        InventoryStylePreset.Comfortable => Comfortable,
        InventoryStylePreset.Classic => Classic,
        InventoryStylePreset.Minimal => Minimal,
        InventoryStylePreset.HighContrast => HighContrast,
        _ => Default,
    };

    public static string GetName(InventoryStylePreset preset)
    {
        foreach (var item in Items)
        {
            if (item.preset == preset) return Localization.Get(item.nameKey);
        }

        return preset.ToString();
    }

    /// <summary>
    /// Stores the preset and restyles every currently open bag window. The window
    /// chrome is built when an addon is allocated, so switching requires a reopen.
    /// </summary>
    public static void Apply(InventoryStylePreset preset)
    {
        if (System.Config is null) return;

        System.Config.General.Style ??= new StyleSettings();
        System.Config.General.Style.Preset = preset;
        Util.SaveConfig(System.Config);

        ReopenOpenWindows();
    }

    private static void ReopenOpenWindows()
    {
        var windows = new KamiToolKit.NativeAddon?[]
        {
            System.AddonInventoryWindow,
            System.AddonSaddleBagWindow,
            System.AddonRetainerWindow,
            System.AddonArmouryWindow,
        };

        foreach (var window in windows)
        {
            if (window is null || !window.IsOpen) continue;

            window.Close();
            Services.Framework.RunOnTick(window.Open, delayTicks: 10);
        }
    }

    #region Presets

    /// <summary>Exactly the look the plugin shipped with before styles existed.</summary>
    public static readonly InventoryStyle Default = new()
    {
        WindowOffsets = new Vector4(64.0f, 32.0f, 32.0f, 32.0f),
        WindowTint = Vector3.Zero,
        WindowAlpha = 1.0f,
        ShowWindowGradation = true,
        ShowDividingLine = true,
        TitleFontType = FontType.TrumpGothic,
        TitleFontSize = 23,
        TitleColorId = 2,
        TitleOutlineColorId = 7,

        LayoutMode = CategoryLayoutMode.Wrapping,
        CategorySpacing = 12.0f,
        CategoryHeaderHeight = 16.0f,
        CategoryHeaderFontSize = 12,
        ItemWidth = 44.0f,
        ItemHeight = 48.0f,
        ItemPadding = 2.0f,
        FooterHeight = 28.0f,
    };

    /// <summary>Tighter frame, smaller slots and tighter gaps: maximum items per screen.</summary>
    public static readonly InventoryStyle Compact = new()
    {
        WindowOffsets = new Vector4(32.0f, 16.0f, 16.0f, 16.0f),
        WindowTint = Vector3.Zero,
        WindowAlpha = 1.0f,
        ShowWindowGradation = true,
        ShowDividingLine = true,
        TitleFontType = FontType.TrumpGothic,
        TitleFontSize = 20,
        TitleColorId = 2,
        TitleOutlineColorId = 7,

        LayoutMode = CategoryLayoutMode.Wrapping,
        CategorySpacing = 6.0f,
        CategoryHeaderHeight = 14.0f,
        CategoryHeaderFontSize = 11,
        ItemWidth = 38.0f,
        ItemHeight = 42.0f,
        ItemPadding = 1.0f,
        FooterHeight = 26.0f,
    };

    /// <summary>Larger slots and generous gaps for readability.</summary>
    public static readonly InventoryStyle Comfortable = new()
    {
        WindowOffsets = new Vector4(64.0f, 32.0f, 32.0f, 32.0f),
        WindowTint = Vector3.Zero,
        WindowAlpha = 1.0f,
        ShowWindowGradation = true,
        ShowDividingLine = true,
        TitleFontType = FontType.TrumpGothic,
        TitleFontSize = 23,
        TitleColorId = 2,
        TitleOutlineColorId = 7,

        LayoutMode = CategoryLayoutMode.Wrapping,
        CategorySpacing = 20.0f,
        CategoryHeaderHeight = 18.0f,
        CategoryHeaderFontSize = 13,
        ItemWidth = 52.0f,
        ItemHeight = 58.0f,
        ItemPadding = 4.0f,
        FooterHeight = 32.0f,
    };

    /// <summary>One category per row, spanning the full width - closest to the vanilla inventory.</summary>
    public static readonly InventoryStyle Classic = new()
    {
        WindowOffsets = new Vector4(64.0f, 32.0f, 32.0f, 32.0f),
        WindowTint = Vector3.Zero,
        WindowAlpha = 1.0f,
        ShowWindowGradation = true,
        ShowDividingLine = true,
        TitleFontType = FontType.TrumpGothic,
        TitleFontSize = 23,
        TitleColorId = 2,
        TitleOutlineColorId = 7,

        LayoutMode = CategoryLayoutMode.SingleColumn,
        CategorySpacing = 8.0f,
        CategoryHeaderHeight = 16.0f,
        CategoryHeaderFontSize = 12,
        ItemWidth = 44.0f,
        ItemHeight = 48.0f,
        ItemPadding = 2.0f,
        FooterHeight = 28.0f,
    };

    /// <summary>Flat, square, chrome-free look with no gradation or divider.</summary>
    public static readonly InventoryStyle Minimal = new()
    {
        WindowOffsets = new Vector4(16.0f, 8.0f, 8.0f, 8.0f),
        WindowTint = Vector3.Zero,
        WindowAlpha = 0.9f,
        ShowWindowGradation = false,
        ShowDividingLine = false,
        TitleFontType = FontType.TrumpGothic,
        TitleFontSize = 20,
        TitleColorId = 2,
        TitleOutlineColorId = 7,

        LayoutMode = CategoryLayoutMode.Wrapping,
        CategorySpacing = 8.0f,
        CategoryHeaderHeight = 14.0f,
        CategoryHeaderFontSize = 11,
        ItemWidth = 40.0f,
        ItemHeight = 44.0f,
        ItemPadding = 2.0f,
        FooterHeight = 26.0f,
    };

    /// <summary>Darker window background so icons and text stand out.</summary>
    public static readonly InventoryStyle HighContrast = new()
    {
        WindowOffsets = new Vector4(64.0f, 32.0f, 32.0f, 32.0f),
        WindowTint = new Vector3(-0.14f, -0.14f, -0.14f),
        WindowAlpha = 1.0f,
        ShowWindowGradation = true,
        ShowDividingLine = true,
        TitleFontType = FontType.TrumpGothic,
        TitleFontSize = 23,
        TitleColorId = 2,
        TitleOutlineColorId = 7,

        LayoutMode = CategoryLayoutMode.Wrapping,
        CategorySpacing = 16.0f,
        CategoryHeaderHeight = 18.0f,
        CategoryHeaderFontSize = 13,
        ItemWidth = 48.0f,
        ItemHeight = 54.0f,
        ItemPadding = 3.0f,
        FooterHeight = 30.0f,
    };

    #endregion
}
