namespace Prismica.Core.Primitives;

public readonly record struct Size(double Width, double Height)
{
    public static readonly Size Zero = new(0, 0);
    public static readonly Size Empty = new(double.NaN, double.NaN);
    public bool IsEmpty => double.IsNaN(Width) || double.IsNaN(Height);
    public bool IsZero => Width == 0 && Height == 0;

    public Size Clamp(Size min, Size max) =>
        new(Math.Clamp(Width, min.Width, max.Width), Math.Clamp(Height, min.Height, max.Height));

    public override string ToString() => $"{Width}x{Height}";

    public static Size operator +(Size a, Size b) => new(a.Width + b.Width, a.Height + b.Height);
    public static Size operator -(Size a, Size b) => new(a.Width - b.Width, a.Height - b.Height);
    public static Size operator *(Size a, double d) => new(a.Width * d, a.Height * d);
}