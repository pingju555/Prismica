using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using CorePoint = Prismica.Core.Primitives.Point;
using CoreRect = Prismica.Core.Primitives.Rect;
using CoreTransform = Prismica.Core.Primitives.Transform;
using CoreCornerRadius = Prismica.Core.Primitives.CornerRadius;
using CoreFontWeight = Prismica.Core.Rendering.FontWeight;
using WpfRect = System.Windows.Rect;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace Prismica.Infra.Wpf;

/// <summary>
/// IRenderContext 的 WPF 实现：把 Core 绘制命令映射到 WPF DrawingContext。
/// </summary>
public sealed class WpfRenderContext : IRenderContext, IDisposable
{
    private readonly DrawingContext _dc;
    private int _depth;
    private readonly WpfSize _scale;

    public WpfRenderContext(DrawingContext drawingContext, CoreRect clipBounds, double dpiScale)
    {
        _dc = drawingContext;
        ClipBounds = clipBounds;
        DpiScale = dpiScale;
        _scale = new WpfSize(1 / dpiScale, 1 / dpiScale);
    }

    public CoreRect ClipBounds { get; }
    public double DpiScale { get; }

    private WpfRect W(CoreRect r) => new(r.X * DpiScale, r.Y * DpiScale, r.Width * DpiScale, r.Height * DpiScale);
    private WpfPoint P(CorePoint p) => new(p.X * DpiScale, p.Y * DpiScale);
    private WpfSize S(double w, double h) => new(w * DpiScale, h * DpiScale);

    public void DrawText(string text, CorePoint position, ArgbColor color, string fontFamily, double fontSize, CoreFontWeight weight = CoreFontWeight.Normal)
    {
        var ft = new FormattedText(
            text, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily(fontFamily), FontStyles.Normal, ToWpfWeight(weight), FontStretches.Normal),
            fontSize * DpiScale, WpfBrushMapper.ToBrush(color), 1.0 * DpiScale);
        _dc.DrawText(ft, P(position));
    }

    public void DrawRect(CoreRect rect, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1)
    {
        if (ClipBounds.Width <= 0 || ClipBounds.Height <= 0) return;
        if (fill.IsTransparent && !stroke.HasValue) return;
        _dc.DrawRectangle(fillBrush(fill), strokeBrush(stroke, strokeWidth), W(rect));
    }

    public void DrawRoundedRect(CoreRect rect, CoreCornerRadius radius, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1)
    {
        var g = new RectangleGeometry(W(rect), radius.TopLeft * DpiScale, radius.TopRight * DpiScale);
        _dc.DrawGeometry(fillBrush(fill), strokeBrush(stroke, strokeWidth), g);
    }

    public void DrawEllipse(CoreRect rect, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1)
    {
        var wrect = W(rect);
        var g = new EllipseGeometry(wrect);
        _dc.DrawGeometry(fillBrush(fill), strokeBrush(stroke, strokeWidth), g);
    }

    public void DrawLine(CorePoint p1, CorePoint p2, ArgbColor color, double thickness = 1)
        => _dc.DrawLine(new Pen(WpfBrushMapper.ToBrush(color), thickness * DpiScale), P(p1), P(p2));

    public void DrawPath(GeometryPath path, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1)
    {
        var g = WpfGeometryPath.ToGeometry(path);
        _dc.DrawGeometry(fillBrush(fill), strokeBrush(stroke, strokeWidth), g);
    }

    public void DrawImage(IImage image, CoreRect destRect, CoreRect? srcRect = null, double opacity = 1)
    {
        BitmapSource? bmp = image switch
        {
            WpfImage w => w.Source,
            BitmapSource b => b,
            _ => null
        };
        if (bmp is null) return;
        _dc.PushOpacity(clamp(opacity));
        if (srcRect.HasValue)
        {
            var s = srcRect.Value;
            var cb = new CroppedBitmap(bmp,
                new Int32Rect((int)s.X, (int)s.Y, Math.Max(1, (int)s.Width), Math.Max(1, (int)s.Height)));
            _dc.DrawImage(cb, W(destRect));
        }
        else
        {
            _dc.DrawImage(bmp, W(destRect));
        }
        _dc.Pop();
    }

    public void PushClip(CoreRect clip) => _dc.PushClip(new RectangleGeometry(W(clip)));
    public void PopClip() => _dc.Pop();

    public void PushOpacity(double opacity) => _dc.PushOpacity(clamp(opacity));
    public void PopOpacity() => _dc.Pop();

    public void PushTransform(CoreTransform transform)
    {
        _dc.PushTransform(ToMatrix(transform));
        _depth++;
    }

    public void PopTransform() { if (_depth > 0) { _dc.Pop(); _depth--; } }

    public void Dispose() { }

    private static System.Windows.Media.Transform ToMatrix(CoreTransform t)
    {
        var m = t.Matrix;
        return new MatrixTransform(m.M11, m.M12, m.M21, m.M22, m.M31, m.M32);
    }

    private static System.Windows.FontWeight ToWpfWeight(CoreFontWeight w) => (int)w switch
    {
        >= 700 => FontWeights.Bold,
        >= 600 => FontWeights.SemiBold,
        >= 500 => FontWeights.Medium,
        >= 300 => FontWeights.Normal,
        _ => FontWeights.Normal
    };

    private static SolidColorBrush fillBrush(ArgbColor c) => WpfBrushMapper.ToBrush(c);

    private static Pen? strokeBrush(ArgbColor? c, double w)
    {
        if (!c.HasValue) return null;
        var pen = new Pen(WpfBrushMapper.ToBrush(c.Value), w);
        pen.Freeze();
        return pen;
    }

    private static double clamp(double d) => Math.Clamp(d, 0, 1);
}