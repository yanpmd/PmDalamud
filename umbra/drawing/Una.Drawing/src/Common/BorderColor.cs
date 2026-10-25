namespace Una.Drawing;

public record struct BorderColor(Color? Top = null, Color? Right = null, Color? Bottom = null, Color? Left = null)
{
    public BorderColor(Color? all) : this(all, all, all, all) { }

    public BorderColor(Color? topRight, Color? bottomLeft) : this(topRight, topRight, bottomLeft, bottomLeft) { }
}
