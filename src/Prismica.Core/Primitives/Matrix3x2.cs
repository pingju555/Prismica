namespace Prismica.Core.Primitives;

/// <summary>
/// 3x2 仿射变换矩阵（二维变换：平移、缩放、旋转、倾斜）。
/// 对应 WPF Matrix / Skia Matrix33 的 2D 子集。
/// </summary>
public readonly record struct Matrix3x2(
    double M11, double M12,
    double M21, double M22,
    double M31, double M32)
{
    public static readonly Matrix3x2 Identity = new(1, 0, 0, 1, 0, 0);

    public bool IsIdentity => this == Identity;

    public Point Transform(Point p) =>
        new(M11 * p.X + M21 * p.Y + M31, M12 * p.X + M22 * p.Y + M32);

    public Rect Transform(Rect r)
    {
        var tl = Transform(r.TopLeft);
        var tr = Transform(r.TopRight);
        var bl = Transform(r.BottomLeft);
        var br = Transform(r.BottomRight);
        double minX = Math.Min(Math.Min(tl.X, tr.X), Math.Min(bl.X, br.X));
        double maxX = Math.Max(Math.Max(tl.X, tr.X), Math.Max(bl.X, br.X));
        double minY = Math.Min(Math.Min(tl.Y, tr.Y), Math.Min(bl.Y, br.Y));
        double maxY = Math.Max(Math.Max(tl.Y, tr.Y), Math.Max(bl.Y, br.Y));
        return new(minX, minY, maxX - minX, maxY - minY);
    }

    public Matrix3x2 Multiply(Matrix3x2 other) =>
        new(
            M11 * other.M11 + M12 * other.M21,
            M11 * other.M12 + M12 * other.M22,
            M21 * other.M11 + M22 * other.M21,
            M21 * other.M12 + M22 * other.M22,
            M31 * other.M11 + M32 * other.M21 + other.M31,
            M31 * other.M12 + M32 * other.M22 + other.M32
        );

    public static Matrix3x2 Translation(double x, double y) => new(1, 0, 0, 1, x, y);
    public static Matrix3x2 Scale(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);
    public static Matrix3x2 Rotation(double radians)
    {
        double c = Math.Cos(radians), s = Math.Sin(radians);
        return new(c, s, -s, c, 0, 0);
    }
    public static Matrix3x2 Skew(double radiansX, double radiansY) =>
        new(1, Math.Tan(radiansY), Math.Tan(radiansX), 1, 0, 0);

    public override string ToString() => $"{M11},{M12},{M21},{M22},{M31},{M32}";
}