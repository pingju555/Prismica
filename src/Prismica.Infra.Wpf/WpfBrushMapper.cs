using System;
using System.Collections.Concurrent;
using System.Windows.Media;
using Prismica.Core.Primitives;

namespace Prismica.Infra.Wpf;

/// <summary>Core ArgbColor ↔ WPF Color/Brush 映射（画刷缓存，Avoiding 重复分配）。</summary>
public static class WpfBrushMapper
{
    private static readonly ConcurrentDictionary<uint, SolidColorBrush> Cache = new();

    public static Color ToColor(ArgbColor c)
        => Color.FromArgb(c.A, c.R, c.G, c.B);

    public static SolidColorBrush ToBrush(ArgbColor c)
        => Cache.GetOrAdd(c.Value, _ =>
        {
            var b = new SolidColorBrush(ToColor(c));
            b.Freeze();
            return b;
        });

    public static Point ToPoint(Core.Primitives.Point p) => new(p.X, p.Y);
    public static Size ToSize(Core.Primitives.Size s) => new(s.Width, s.Height);
    public static Rect ToRect(Core.Primitives.Rect r) => new(r.X, r.Y, r.Width, r.Height);
    public static System.Windows.Rect ToWpfRect(Core.Primitives.Rect r) => new(r.X, r.Y, r.Width, r.Height);
}