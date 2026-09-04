using System;
using System.Collections.Generic;
using Prismica.Core.Primitives;

namespace Prismica.Core.Rendering;

public interface IRenderContext
{
    Rect ClipBounds { get; }
    double DpiScale { get; }
    void DrawText(string text, Point position, ArgbColor color, string fontFamily, double fontSize, FontWeight weight = FontWeight.Normal);
    void DrawRect(Rect rect, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1);
    void DrawRoundedRect(Rect rect, CornerRadius radius, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1);
    void DrawEllipse(Rect rect, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1);
    void DrawLine(Point p1, Point p2, ArgbColor color, double thickness = 1);
    void DrawPath(GeometryPath path, ArgbColor fill, ArgbColor? stroke = null, double strokeWidth = 1);
    void DrawImage(IImage image, Rect destRect, Rect? srcRect = null, double opacity = 1);
    void PushClip(Rect clip);
    void PopClip();
    void PushOpacity(double opacity);
    void PopOpacity();
    void PushTransform(Transform transform);
    void PopTransform();
}

public enum FontWeight { Thin = 100, ExtraLight = 200, Light = 300, Normal = 400, Medium = 500, SemiBold = 600, Bold = 700, ExtraBold = 800, Black = 900 }

public interface IImage : IDisposable
{
    Size Size { get; }
    double DpiX { get; }
    double DpiY { get; }
}

public interface GeometryPath
{
    void MoveTo(Point p);
    void LineTo(Point p);
    void CubicBezierTo(Point c1, Point c2, Point p);
    void QuadraticBezierTo(Point c, Point p);
    void ArcTo(Point p, Size radius, double rotation, bool isLargeArc, bool sweepClockwise);
    void Close();
}