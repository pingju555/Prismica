namespace Prismica.Core.Primitives;

public readonly record struct Rect(double X, double Y, double Width, double Height)
{
    public static readonly Rect Empty = new(0, 0, 0, 0);

    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public Point TopLeft => new(X, Y);
    public Point TopRight => new(Right, Y);
    public Point BottomLeft => new(X, Bottom);
    public Point BottomRight => new(Right, Bottom);
    public Point Center => new(X + Width / 2, Y + Height / 2);
    public Size Size => new(Width, Height);
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(Point p) => p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;
    public bool Contains(Rect r) => X <= r.X && Right >= r.Right && Y <= r.Y && Bottom >= r.Bottom;
    public bool IntersectsWith(Rect r) => X < r.Right && Right > r.X && Y < r.Bottom && Bottom > r.Y;

    public Rect Intersect(Rect r)
    {
        double left = Math.Max(X, r.X);
        double top = Math.Max(Y, r.Y);
        double right = Math.Min(Right, r.Right);
        double bottom = Math.Min(Bottom, r.Bottom);
        return right > left && bottom > top ? new(left, top, right - left, bottom - top) : Empty;
    }

    public Rect Union(Rect r)
    {
        if (IsEmpty) return r;
        if (r.IsEmpty) return this;
        double left = Math.Min(X, r.X);
        double top = Math.Min(Y, r.Y);
        double right = Math.Max(Right, r.Right);
        double bottom = Math.Max(Bottom, r.Bottom);
        return new(left, top, right - left, bottom - top);
    }

    public Rect Inflate(double dx, double dy) => new(X - dx, Y - dy, Width + 2 * dx, Height + 2 * dy);
    public Rect Offset(double dx, double dy) => new(X + dx, Y + dy, Width, Height);

    public override string ToString() => $"{X},{Y} {Width}x{Height}";
}