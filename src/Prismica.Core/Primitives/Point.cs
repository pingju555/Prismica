namespace Prismica.Core.Primitives;

/// <summary>
/// 二维点（设备无关像素 DIP）。
/// </summary>
public readonly record struct Point(double X, double Y)
{
    public static readonly Point Zero = new(0, 0);

    public Point Offset(double dx, double dy) => new(X + dx, Y + dy);
    public Point Multiply(double factor) => new(X * factor, Y * factor);

    public double DistanceTo(Point other) => Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));

    public override string ToString() => $"{X},{Y}";

    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
    public static Point operator *(Point a, double d) => new(a.X * d, a.Y * d);
}