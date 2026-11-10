using System.Numerics;
using Dalamud.Interface;
using KamiToolKit.Nodes.Simplified;

namespace AetherBags.Compat;

/// <summary>
///     A simple image node that makes it easy to display a single color.
/// </summary>
public unsafe class BackgroundImageNode : SimpleImageNode {
    public BackgroundImageNode() {
        FitTexture = true;
    }

    public new Vector4 Color {
        get => new(AddColor.X, AddColor.Y, AddColor.Z, Alpha / 255.0f);
        set {
            MultiplyColor = Vector3.Zero;
            Alpha = value.W;
            AddColor = value.AsVector3Color();
        }
    }

    public new ColorHelpers.HsvaColor ColorHsva {
        get => ColorHelpers.RgbaToHsv(Color);
        set => Color = ColorHelpers.HsvToRgb(value);
    }
}
