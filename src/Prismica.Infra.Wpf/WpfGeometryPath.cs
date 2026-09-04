using System;
using System.Windows.Media;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using CoreTransform = Prismica.Core.Primitives.Transform;
using CorePoint = Prismica.Core.Primitives.Point;
using CoreSize = Prismica.Core.Primitives.Size;

namespace Prismica.Infra.Wpf;

/// <summary>
/// GeometryPath 的 WPF 实现：录制路径点到 StreamGeometry。
/// 供客户端作为描述子构建，再交给 DrawPath。
/// </summary>
public sealed class WpfGeometryPath : GeometryPath, IDisposable
{
    private readonly StreamGeometry _geometry;
    private StreamGeometryContext? _ctx;

    public WpfGeometryPath()
    {
        _geometry = new StreamGeometry();
        _ctx = _geometry.Open();
    }

    private StreamGeometryContext Ctx => _ctx ?? throw new ObjectDisposedException(nameof(WpfGeometryPath));

    public void MoveTo(CorePoint p)
        => Ctx.BeginFigure(new System.Windows.Point(p.X, p.Y), /*isFilled*/ true, /*isClosed*/ false);

    public void LineTo(CorePoint p) => Ctx.LineTo(new System.Windows.Point(p.X, p.Y), true, false);

    public void CubicBezierTo(CorePoint c1, CorePoint c2, CorePoint p)
        => Ctx.BezierTo(new System.Windows.Point(c1.X, c1.Y), new System.Windows.Point(c2.X, c2.Y), new System.Windows.Point(p.X, p.Y), true, false);

    public void QuadraticBezierTo(CorePoint c, CorePoint p)
        => Ctx.QuadraticBezierTo(new System.Windows.Point(c.X, c.Y), new System.Windows.Point(p.X, p.Y), true, false);

    public void ArcTo(CorePoint p, CoreSize radius, double rotation, bool isLargeArc, bool sweepClockwise)
        => Ctx.ArcTo(new System.Windows.Point(p.X, p.Y), new System.Windows.Size(radius.Width, radius.Height), rotation * 180.0 / Math.PI, isLargeArc, sweepClockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true, false);

    public void Close() => Ctx.Close();

    public void Dispose()
    {
        _ctx?.Close();
        _ctx = null;
        _geometry.Freeze();
    }

    /// <summary>终结并返回冻结的 StreamGeometry。</summary>
    public StreamGeometry ToGeometry()
    {
        if (_ctx is not null) Dispose();
        return _geometry;
    }

    public static StreamGeometry ToGeometry(GeometryPath path)
    {
        if (path is WpfGeometryPath wg) return wg.ToGeometry();
        throw new NotSupportedException("只支持 WpfGeometryPath 作为 GeometryPath 实现。");
    }
}