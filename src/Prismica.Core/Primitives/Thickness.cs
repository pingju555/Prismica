namespace Prismica.Core.Primitives;

public readonly record struct Thickness(double Left, double Top, double Right, double Bottom)
{
    public static readonly Thickness Zero = new(0, 0, 0, 0);
    public static Thickness Uniform(double v) => new(v, v, v, v);

    public double Horizontal => Left + Right;
    public double Vertical => Top + Bottom;

    public Rect Inflate(Rect r) => new(r.X - Left, r.Y - Top, r.Width + Horizontal, r.Height + Vertical);
    public Rect Deflate(Rect r) => new(r.X + Left, r.Y + Top, r.Width - Horizontal, r.Height - Vertical);

    public override string ToString() => $"{Left},{Top},{Right},{Bottom}";
}