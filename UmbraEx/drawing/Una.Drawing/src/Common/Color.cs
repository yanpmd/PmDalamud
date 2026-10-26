using System.Linq;

namespace Una.Drawing;

public struct Color(byte r, byte g, byte b, byte a = 255)
{
    /// <summary>
    /// Specifies the name of this color.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Represents the current version of the color theme.
    /// </summary>
    public static uint ThemeVersion { get; private set; } = 0;

    private static readonly Dictionary<string, uint> NamedColors = [];

    private byte _r = r;
    private byte _g = g;
    private byte _b = b;
    private byte _a = a;

    /// <summary>
    /// Returns the red component of this color as a byte.
    /// </summary>
    public byte R {
        set => _r = value;
        get {
            if (Name != string.Empty && NamedColors.TryGetValue(Name, out uint color)) {
                return (byte)(color >> 16);
            }

            return _r;
        }
    }

    /// <summary>
    /// Returns the green component of this color as a byte.
    /// </summary>
    public byte G {
        set => _g = value;
        get {
            if (Name != string.Empty && NamedColors.TryGetValue(Name, out uint color)) {
                return (byte)(color >> 8);
            }

            return _g;
        }
    }

    /// <summary>
    /// Returns the blue component of this color as a byte.
    /// </summary>
    public byte B {
        set => _b = value;
        get {
            if (Name != string.Empty && NamedColors.TryGetValue(Name, out uint color)) {
                return (byte)(color);
            }

            return _b;
        }
    }

    /// <summary>
    /// Returns the alpha component of this color as a float.
    /// </summary>
    public byte A {
        set => _a = value;
        get {
            if (Name != string.Empty && NamedColors.TryGetValue(Name, out uint color)) {
                return (byte)(color >> 24);
            }

            return _a;
        }
    }

    /// <summary>
    /// Assigns a named color to a specific color.
    /// </summary>
    public static void AssignByName(string name, uint color)
    {
        NamedColors[name] = color;
        ThemeVersion++;

        if (ThemeVersion >= uint.MaxValue - 1) {
            ThemeVersion = 1;
        }
    }

    /// <summary>
    /// Returns a list of all assigned color names.
    /// </summary>
    public static List<string> GetAssignedNames()
    {
        return NamedColors.Keys.ToList();
    }

    /// <summary>
    /// Returns the named color as a UInt32.
    /// </summary>
    public static uint GetNamedColor(string name)
    {
        return NamedColors.TryGetValue(name, out uint color) ? color : 0;
    }

    /// <summary>
    /// Returns a UInt32 representation of the color in ABGR format.
    /// </summary>
    /// <returns></returns>
    public uint ToUInt()
    {
        if (Name != string.Empty && NamedColors.TryGetValue(Name, out uint color)) {
            return color;
        }

        return (uint)(A << 24 | R << 16 | G << 8 | B);
    }

    public Color(byte r, byte g, byte b, float a) : this(r, g, b, (byte)(a / 255)) { }

    /// <summary>
    /// Constructs a color from a UInt32 value.
    /// </summary>
    /// <param name="color">The color value in 0xAABBGGRR format.</param>
    public Color(uint color) : this(
        (byte)((color >> 16) & 0xFF),
        (byte)((color >> 8) & 0xFF),
        (byte)((color) & 0xFF),
        (byte)((color >> 24) & 0xFF)
    ) { }

    public Color(string name) : this(0, 0, 0, 0)
    {
        Name = name;
    }

    /// <summary>
    /// Returns true if this color is visible.
    /// </summary>
    public bool IsVisible => A > 0;

    public override string ToString()          => $"Color(#{ToUInt():x8})";
    public override bool   Equals(object? obj) => obj is Color color && color.ToUInt() == ToUInt();
    public override int    GetHashCode()       => ToUInt().GetHashCode();

    public static bool operator ==(Color? left, Color? right) => left is not null && left.Equals(right);
    public static bool operator !=(Color? left, Color? right) => !(left == right);

    public Color Copy() => new(R, G, B, A);
    
    internal static SKColor ToSkColor(Color? color)
    {
        return color is null
            ? SKColor.Empty
            : new(color.Value.R, color.Value.G, color.Value.B, color.Value.A);
    }
}