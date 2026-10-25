namespace Una.Drawing;

public readonly struct Point(int x, int y) : IEquatable<Point>
{
    public readonly int X = x;
    public readonly int Y = y;

    public bool Equals(Point other)
    {
        return X == other.X
         && Y    == other.Y;
    }

    public override bool Equals(object? obj)
    {
        return obj is Point other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public static bool operator ==(Point left, Point right) => left.X == right.X && left.Y == right.Y;
    public static bool operator !=(Point left, Point right) => left.X != right.X || left.Y != right.Y;
}
