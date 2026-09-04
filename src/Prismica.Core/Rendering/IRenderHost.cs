using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Prismica.Core.Primitives;
using Prismica.Core.Components;
using Prismica.Core.Formula;
using Prismica.Core.Actions;

namespace Prismica.Core.Rendering;

public interface IRenderHost : IDisposable
{
    IVisualRoot CreateVisualRoot(ComponentDefinition def, RenderContext ctx);
    void UpdateVisual(IVisualRoot root, ParameterOverride overrides);
    void ArrangeLayout(IVisualRoot root, Rect finalRect);
    HitTestResult HitTest(IVisualRoot root, Point point);
    Task<byte[]> CaptureAsync(IVisualRoot root, ImageFormat format = ImageFormat.Png);
}

public interface IVisualRoot : IVisual
{
    ComponentDefinition Definition { get; }
    RenderContext Context { get; }
    void InvalidateVisual();
    void InvalidateMeasure();
    void InvalidateArrange();
}

public interface IVisual
{
    Rect Bounds { get; }
    Transform Transform { get; set; }
    double Opacity { get; set; }
    bool IsVisible { get; set; }
    IVisual? Parent { get; }
    IReadOnlyList<IVisual> Children { get; }
    HitTestResult HitTest(Point point);
}

public sealed record RenderContext(
    IFormulaEngine FormulaEngine,
    IReadOnlyDictionary<string, ArgbColor> GlobalVariables,
    double DpiScale,
    Size ViewportSize
);

public sealed record ParameterOverride(
    IReadOnlyDictionary<string, object> Values
);

public enum ImageFormat { Png, Jpeg, Bmp }