namespace Una.Drawing;

[Flags]
public enum RoundedCorners : byte
{
    None        = 0,
    TopLeft     = 1,
    TopRight    = 2,
    BottomLeft  = 4,
    BottomRight = 8,

    All = TopLeft | TopRight | BottomLeft | BottomRight
}