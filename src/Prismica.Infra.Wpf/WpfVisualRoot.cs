using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Prismica.Core.Components;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using CoreTransform = Prismica.Core.Primitives.Transform;
using CorePoint = Prismica.Core.Primitives.Point;
using CoreRect = Prismica.Core.Primitives.Rect;
using CoreResult = Prismica.Core.Components.HitTestResult;
using WpfSize = System.Windows.Size;

namespace Prismica.Infra.Wpf;

/// <summary>
/// IVisualRoot 的 WPF 承载：一个 FrameworkElement，把 Core 视觉树渲染到 DrawingContext。
/// 由 ComponentDefinition 播种内置 Meter 视觉（原型切片）。
/// 相关 Core 成员用显式接口实现，避免与 FrameworkElement 基类成员冲突。
/// </summary>
public sealed class WpfVisualRoot : FrameworkElement, IVisualRoot
{
    private readonly List<IVisual> _children = new();
    private IReadOnlyList<Prismica.Core.Meters.IMeter>? _runtimeMeters;
    private IReadOnlyList<IEmbedHost>? _runtimeEmbeds;
    private CoreRect _bounds;
    private CoreTransform _transform = CoreTransform.Identity;

    public WpfVisualRoot(ComponentDefinition definition, RenderContext context)
    {
        Definition = definition;
        Context = context;

        foreach (var meter in definition.Meters)
        {
            var visual = new WpfMeterVisual(meter) { Parent = this };
            _children.Add(visual);
        }

        var ps = definition.Prismica;
        Width = ps.Width > 0 ? ps.Width : 800;
        Height = ps.Height > 0 ? ps.Height : 600;
        _bounds = new CoreRect(0, 0, Width, Height);
    }

    /// <summary>
    /// 运行时模式：渲染 Core IMeter 树（ComponentRuntime 产出），弃用逐 MeterDefinition 的 WpfMeterVisual 切片。
    /// 仍是一个 WpfVisualRoot，因此 WpfOverlayWindow 的内容区命中/SetRoot 兼容不变。
    /// </summary>
    public WpfVisualRoot(ComponentDefinition definition, RenderContext context,
        IReadOnlyList<Prismica.Core.Meters.IMeter> meters,
        IReadOnlyList<IEmbedHost>? embeds = null)
        : this(definition, context)
    {
        _runtimeMeters = meters;
        _runtimeEmbeds = embeds;
    }

    public ComponentDefinition Definition { get; }
    public RenderContext Context { get; }

    // ---- IVisual / IVisualRoot 显式实现 ----
    CoreRect IVisual.Bounds => _bounds;

    CoreTransform IVisual.Transform
    {
        get => _transform;
        set { _transform = value; base.InvalidateVisual(); }
    }

    double IVisual.Opacity { get; set; } = 1;

    bool IVisual.IsVisible { get; set; } = true;

    IVisual? IVisual.Parent { get; } = null;

    void IVisualRoot.InvalidateVisual() => base.InvalidateVisual();
    void IVisualRoot.InvalidateMeasure() => base.InvalidateMeasure();
    void IVisualRoot.InvalidateArrange() => base.InvalidateArrange();

    public IReadOnlyList<IVisual> Children => _children;

    /// <summary>
    /// 内容命中检测：仅当点落在某个可见 meter（内容区域）内才返回 true。
    /// 与 HitTest 不同——HitTest 对整窗 bounds 都返回 Hit=true，无法区分透明空区。
    /// 供覆盖窗口 WM_NCHITTEST 判定"可点击 vs 穿透"。
    /// </summary>
    public bool HitTestContent(CorePoint point)
    {
        if (_runtimeMeters is not null)
        {
            foreach (var meter in _runtimeMeters)
            {
                var r = meter.Layout;
                if (r == null) continue;
                if (new CoreRect(r.X, r.Y, r.Width, r.Height).Contains(point)) return true;
            }
            return false;
        }
        foreach (var child in _children)
        {
            if (child.IsVisible && child.HitTest(point).Hit) return true;
        }
        return false;
    }

    /// <summary>按 meter 名称更新实时文本（帧调度驱动）。</summary>
    public bool SetMeterText(string name, string text)
    {
        foreach (var child in _children)
        {
            if (child is WpfMeterVisual mv && string.Equals(mv.MeterName, name, StringComparison.OrdinalIgnoreCase))
            {
                mv.SetText(text);
                base.InvalidateVisual();
                return true;
            }
        }
        return false;
    }

    public CoreResult HitTest(CorePoint point)
    {
        foreach (var child in _children)
        {
            if (child.IsVisible && child.Bounds.Contains(point))
                return child.HitTest(point);
        }
        return _bounds.Contains(point)
            ? new CoreResult(true, null, HitTestAction.Click, point)
            : new CoreResult(false, null, HitTestAction.None, point);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        double w = ActualWidth > 0 ? ActualWidth : Width;
        double h = ActualHeight > 0 ? ActualHeight : Height;
        var clip = new CoreRect(0, 0, w, h);

        using var ctx = new WpfRenderContext(drawingContext, clip, Context.DpiScale);
        if (_runtimeMeters is not null)
        {
            foreach (var meter in _runtimeMeters)
                meter.Render(ctx);
            if (_runtimeEmbeds is not null)
                foreach (var embed in _runtimeEmbeds)
                    embed.Render(this, Context, ctx);
            return;
        }
        foreach (var child in _children)
        {
            if (child is WpfMeterVisual mv) mv.Draw(ctx);
        }
    }

    protected override WpfSize MeasureOverride(WpfSize availableSize)
    {
        double w = ActualWidth > 0 ? ActualWidth : Width;
        double h = ActualHeight > 0 ? ActualHeight : Height;
        return new WpfSize(w, h);
    }
}