using System;
using System.Collections.Generic;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Prismica.Core.Measures;

namespace Prismica.Core.Meters;

public abstract class MeterBase : IMeter
{
    public string Name { get; }
    public abstract MeterTypeInfo TypeInfo { get; }
    public MeterLayout Layout { get; set; } = new(0, 0, 0, 0);
    public MeterStyle? Style { get; set; }
    public IReadOnlyList<string> BoundMeasureNames { get; protected set; } = Array.Empty<string>();
    private bool _disposed;
    private IReadOnlyDictionary<string, string> _fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private Prismica.Core.Primitives.Rect _layoutRect;

    protected MeterBase(string name) => Name = name;

    public virtual void Configure(IReadOnlyDictionary<string, string> fields)
    {
        _fields = new Dictionary<string, string>(fields, StringComparer.OrdinalIgnoreCase);
        ApplyLayout(Get("X"), Get("Y"), Get("W"), Get("H"));
    }

    public abstract ValueTask UpdateAsync(MeterContext ctx, CancellationToken ct = default);
    public abstract void Render(IRenderContext renderCtx);

    protected string? Get(string key)
        => _fields.TryGetValue(key, out var v) ? v : (_fields.TryGetValue(key.ToLowerInvariant(), out var l) ? l : null);

    protected static double D(string? s, double fallback)
        => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

    protected static ArgbColor ColorOr(string? a, string? b, ArgbColor fallback)
    {
        foreach (var s in new[] { a, b })
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            try { return ArgbColor.FromHex(s); } catch { }
        }
        return fallback;
    }

    protected void ApplyLayout(string? x, string? y, string? w, string? h)
    {
        double W = D(w, 100), H = D(h, 40);
        if (W <= 0) W = 100;
        if (H <= 0) H = 40;
        _layoutRect = new Prismica.Core.Primitives.Rect(D(x, 0), D(y, 0), W, H);
        Layout = new MeterLayout(_layoutRect.X, _layoutRect.Y, _layoutRect.Width, _layoutRect.Height);
    }

    protected Prismica.Core.Primitives.Rect LayoutRect() => _layoutRect;

    protected (string face, double size, ArgbColor color, ArgbColor bg) ResolveTextStyle(string? face, string? fs, string? color, string? bg)
        => (face ?? "Segoe UI", D(fs, 14), ColorOr(color, null, ArgbColor.White), ColorOr(bg, null, ArgbColor.Transparent));

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            DisposeCore();
        }
    }

    protected virtual void DisposeCore() { }
}

public sealed class StringMeter : MeterBase
{
    public override MeterTypeInfo TypeInfo => new(
        "String", "文本", "Text",
        new Dictionary<string, MeterFieldInfo>
        {
            ["FontFace"] = new("FontFace", "font", "Segoe UI", "字体", true),
            ["FontSize"] = new("FontSize", "number", "14", "字号", true),
            ["FontColor"] = new("FontColor", "color", "#FFFFFFFF", "字体颜色", true),
            ["FontWeight"] = new("FontWeight", "string", "Normal", "字重", false),
            ["StringAlign"] = new("StringAlign", "string", "Left", "Left/Center/Right", false),
            ["ClipString"] = new("ClipString", "bool", "false", "截断过长文本", false),
            ["Text"] = new("Text", "string", "", "文本内容（含变量/公式/Measure 引用）", true)
        },
        true, true, "单行/多行文本"
    );

    private string _fontFace = "Segoe UI";
    private double _fontSize = 14;
    private ArgbColor _fontColor = ArgbColor.White;
    private ArgbColor _background = ArgbColor.Transparent;
    private string? _measureName;
    private bool _clipString;

    public StringMeter(string name) : base(name) { }

    /// <summary>本次更新解析出的可见文本（供渲染/测试读取）。</summary>
    public string RenderedText { get; private set; } = "";

    public override ValueTask UpdateAsync(MeterContext ctx, CancellationToken ct = default)
    {
        // 布局与样式字段（大小写不敏感，支持 X=... Y=... W=... H=... 单行多键）。
        ApplyLayout(Get("X"), Get("Y"), Get("W"), Get("H"));

        var (face, size, color, bg) = ResolveTextStyle(Get("FontFace"), Get("FontSize"), Get("FontColor"), Get("Color") ?? Get("SolidColor"));
        _fontFace = face; _fontSize = size; _fontColor = color; _background = bg;
        _clipString = bool.TryParse(Get("ClipString"), out var cb) && cb;

        _measureName = Get("MeasureName") ?? Get("Measure");
        string rawText = Get("Text") ?? Get("String") ?? "";

        // 优先：绑定 Measure，取其当前值文本。
        if (_measureName != null && ctx.Measures.TryGetValue(_measureName, out var measure) && measure.CurrentValue.HasValue)
        {
            RenderedText = FormatValue(measure.CurrentValue);
        }
        else if (rawText.StartsWith('[')) // [MeasureName]
        {
            string s = rawText.Trim('[', ']');
            RenderedText = ctx.Measures.TryGetValue(s, out var m2) && m2.CurrentValue.HasValue ? FormatValue(m2.CurrentValue) : "";
        }
        else
        {
            // 变量替换：#Var#（组件自身颜色变量，来自 [Variables]）+ #gv:Name#（跨组件共享全局变量，来自共享 GlobalVariableStore）。
            string t = rawText;
            if (ctx.Variables.Count > 0)
            {
                foreach (var (k, v) in ctx.Variables)
                    t = t.Replace($"#{k}#", v.ToHex());
            }
            if (ctx.Globals.Count > 0)
            {
                foreach (var (k, v) in ctx.Globals)
                    t = t.Replace($"#gv:{k}#", v);
            }
            RenderedText = _clipString && t.Length > 64 ? t[..64] + "…" : t;
        }

        BoundMeasureNames = _measureName != null ? new[] { _measureName } : Array.Empty<string>();
        return ValueTask.CompletedTask;
    }

    public override void Render(IRenderContext renderCtx)
    {
        var rect = LayoutRect();
        if (!_background.IsTransparent)
            renderCtx.DrawRoundedRect(rect, CornerRadius.Uniform(UI_RADIUS), _background);
        if (!string.IsNullOrEmpty(RenderedText))
            renderCtx.DrawText(RenderedText, new Point(rect.X + 4, rect.Y + 4), _fontColor, _fontFace, _fontSize);
    }

    private static string FormatValue(MeasureValue v)
    {
        if (v.String != null) return v.String;
        if (v.Number.HasValue) return v.Number.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return "";
    }

    private const double UI_RADIUS = 4;
}

public sealed class ProgressMeter : MeterBase
{
    public override MeterTypeInfo TypeInfo => new(
        "Progress", "进度条", "Progress",
        new Dictionary<string, MeterFieldInfo>
        {
            ["BarColor"] = new("BarColor", "color", "#00FF88", "进度条颜色", true),
            ["BackgroundColor"] = new("BackgroundColor", "color", "#40000000", "背景色", true),
            ["BorderColor"] = new("BorderColor", "color", "#00000000", "边框色", false),
            ["BorderWidth"] = new("BorderWidth", "number", "1", "边框宽度", false),
            ["Radius"] = new("Radius", "number", "0", "圆角半径", false),
            ["Orientation"] = new("Orientation", "string", "Horizontal", "Horizontal/Vertical/Radial", false),
            ["Invert"] = new("Invert", "bool", "false", "反向", false),
            ["Animation"] = new("Animation", "bool", "true", "动画", false)
        },
        true, true, "线性/环形进度条"
    );

    private ArgbColor _barColor = new(0xFF00FF88);
    private ArgbColor _bgColor = new(0x40000000);
    private bool _vertical;
    private bool _invert;
    private double _value; // 0..1

    public ProgressMeter(string name) : base(name) { }

    /// <summary>本次更新解析出的 0..100 显示值。</summary>
    public double DisplayValue { get; private set; }

    public override ValueTask UpdateAsync(MeterContext ctx, CancellationToken ct = default)
    {
        ApplyLayout(Get("X"), Get("Y"), Get("W"), Get("H"));
        _barColor = ColorOr(Get("BarColor"), null, new ArgbColor(0xFF00FF88));
        _bgColor = ColorOr(Get("BackgroundColor"), Get("Color"), new ArgbColor(0x40000000));
        _vertical = StringComparer.OrdinalIgnoreCase.Equals(Get("Orientation"), "Vertical");
        _invert = bool.TryParse(Get("Invert"), out var inv) && inv;

        string? measureName = Get("MeasureName") ?? Get("Measure");
        BoundMeasureNames = measureName != null ? new[] { measureName } : Array.Empty<string>();

        double raw = 0;
        if (measureName != null && ctx.Measures.TryGetValue(measureName, out var m) && m.CurrentValue.Number.HasValue)
            raw = m.CurrentValue.Number.Value;

        double norm = Math.Clamp(raw / 100.0, 0, 1);
        if (_invert) norm = 1 - norm;
        _value = norm;
        DisplayValue = raw;
        return ValueTask.CompletedTask;
    }

    public override void Render(IRenderContext renderCtx)
    {
        var rect = LayoutRect();
        var radius = CornerRadius.Uniform(UI_RADIUS);
        if (!_bgColor.IsTransparent)
            renderCtx.DrawRoundedRect(rect, radius, _bgColor);

        double w = rect.Width, h = rect.Height;
        double fx = _vertical ? 1 : _value;
        double fy = _vertical ? _value : 1;
        if (fx <= 0 || fy <= 0) return;

        var bar = new Prismica.Core.Primitives.Rect(rect.X, rect.Y, w * fx, h * fy);
        renderCtx.DrawRoundedRect(bar, radius, _barColor);
    }

    private const double UI_RADIUS = 4;
}

public sealed class ContainerMeter : MeterBase
{
    public override MeterTypeInfo TypeInfo => new(
        "Container", "容器", "Container",
        new Dictionary<string, MeterFieldInfo>
        {
            ["ClipToBounds"] = new("ClipToBounds", "bool", "true", "裁剪子元素", false),
            ["Layout"] = new("Layout", "string", "Canvas", "Canvas/Stack/Grid", false)
        },
        false, true, "视觉容器"
    );

    public ContainerMeter(string name) : base(name) { }

    public override ValueTask UpdateAsync(MeterContext ctx, CancellationToken ct = default) => ValueTask.CompletedTask;
    public override void Render(IRenderContext renderCtx) { }
}