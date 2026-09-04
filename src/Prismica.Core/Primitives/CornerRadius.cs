namespace Prismica.Core.Primitives;

public readonly record struct CornerRadius(double TopLeft, double TopRight, double BottomRight, double BottomLeft)
{
    public static readonly CornerRadius Zero = new(0, 0, 0, 0);
    public static CornerRadius Uniform(double r) => new(r, r, r, r);

    public bool IsZero => TopLeft == 0 && TopRight == 0 && BottomRight == 0 && BottomLeft == 0;
    public bool IsUniform => TopLeft == TopRight && TopRight == BottomRight && BottomRight == BottomLeft;

    public override string ToString() => IsUniform ? TopLeft.ToString() : $"{TopLeft},{TopRight},{BottomRight},{BottomLeft}";
}