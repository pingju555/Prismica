using System;
using System.Collections.Generic;
using System.Globalization;
using Prismica.Core.Components;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using CoreTransform = Prismica.Core.Primitives.Transform;
using CorePoint = Prismica.Core.Primitives.Point;

namespace Prismica.Infra.Wpf;

/// <summary>
/// 由 MeterDefinition 构造的简单可视化（原型切片）。
/// 支持基本的矩形/圆角矩形 + 可选文本渲染，供 IRenderHost 纵向打通。
/// </summary>
public sealed class WpfMeterVisual : IVisual
{
    private readonly MeterDefinition _def;

    public WpfMeterVisual(MeterDefinition def)
    {
        _def = def;
        _ = double.TryParse(Get("X"), NumberStyles.Float, CultureInfo.InvariantCulture, out double x);
        _ = double.TryParse(Get("Y"), NumberStyles.Float, CultureInfo.InvariantCulture, out double y);
        _ = double.TryParse(Get("W"), NumberStyles.Float, CultureInfo.InvariantCulture, out double w);
        _ = double.TryParse(Get("H"), NumberStyles.Float, CultureInfo.InvariantCulture, out double h);
        Bounds = new Rect(x, y, w > 0 ? w : 100, h > 0 ? h : 40);

        Fill = ParseColor(Get("Color"), Get("SolidColor"), ArgbColor.Transparent);
        Text = Get("Text") ?? Get("String") ?? "";
        FontFamily = Get("Font") ?? "Segoe UI";
        _ = double.TryParse(Get("FontSize"), NumberStyles.Float, CultureInfo.InvariantCulture, out double fs);
        FontSize = fs > 0 ? fs : 14;
    }

    public Rect Bounds { get; }
    public CoreTransform Transform { get; set; } = CoreTransform.Identity;
    public double Opacity { get; set; } = 1;
    public bool IsVisible { get; set; } = true;
    public IVisual? Parent { get; set; }
    public IReadOnlyList<IVisual> Children => Array.Empty<IVisual>();

    public string MeterName => _def.Name;
    public string Text { get; private set; }
    public string FontFamily { get; }
    public double FontSize { get; }
    public ArgbColor Fill { get; }

    /// <summary>更新实时文本（由帧调度驱动，用于 data-track 动态刷新）。</summary>
    public void SetText(string text)
    {
        Text = text;
    }

    public void Draw(WpfRenderContext ctx)
    {
        if (!IsVisible || Opacity <= 0) return;
        if (!Fill.IsTransparent)
        {
            ctx.PushOpacity(Opacity);
            ctx.DrawRoundedRect(Bounds, new CornerRadius(0, 0, 0, 0), Fill);
            ctx.PopOpacity();
        }
        if (!string.IsNullOrEmpty(Text))
        {
            ctx.PushOpacity(Opacity);
            ctx.DrawText(Text, new CorePoint(Bounds.X + 4, Bounds.Y + 4), ArgbColor.Black, FontFamily, FontSize);
            ctx.PopOpacity();
        }
    }

    public HitTestResult HitTest(CorePoint point)
        => Bounds.Contains(point) ? new HitTestResult(true, _def.Name, HitTestAction.Click, point) : new HitTestResult(false, null, HitTestAction.None, point);

    private string? Get(string key)
        => _def.Fields.TryGetValue(key, out var v) ? v : (_def.Fields.TryGetValue(key.ToLowerInvariant(), out var l) ? l : null);

    private static ArgbColor ParseColor(string? a, string? b, ArgbColor fallback)
    {
        foreach (var s in new[] { a, b })
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            try { return ArgbColor.FromHex(s); } catch { }
        }
        return fallback;
    }
}