namespace Prismica.Core.Primitives;

/// <summary>
/// 变换封装：可分解为平移/缩放/旋转/倾斜，也可直接用矩阵。
/// </summary>
public readonly record struct Transform(Matrix3x2 Matrix)
{
    public static readonly Transform Identity = new(Matrix3x2.Identity);

    public bool IsIdentity => Matrix.IsIdentity;

    public Point TransformPoint(Point p) => Matrix.Transform(p);
    public Rect TransformRect(Rect r) => Matrix.Transform(r);

    public static Transform CreateTranslation(double x, double y) => new(Matrix3x2.Translation(x, y));
    public static Transform CreateScale(double sx, double sy) => new(Matrix3x2.Scale(sx, sy));
    public static Transform CreateRotation(double radians) => new(Matrix3x2.Rotation(radians));
    public static Transform CreateSkew(double rx, double ry) => new(Matrix3x2.Skew(rx, ry));

    public static Transform operator *(Transform a, Transform b) => new(a.Matrix.Multiply(b.Matrix));
}