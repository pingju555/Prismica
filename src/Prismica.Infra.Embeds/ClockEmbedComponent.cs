using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Prismica.Core.Components;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Prismica.Core.Scheduling;

namespace Prismica.Infra.Embeds;

/// <summary>
/// 首个 EMBED 单元：Clock（WP5 首批）。
/// 提供时间跟踪的 Host；Render 接入 embed-track 渲染面（当前为占位说明，绘制由上层管线接入）。
/// </summary>
public sealed class ClockEmbedComponent : IEmbedComponent
{
    public string Keyword => "Clock";
    public EmbedCapabilities Capabilities => EmbedCapabilities.Animatable;
    public Size DefaultSize => new(200, 60);

    public IEmbedHost CreateHost(EmbedDefinition def, EmbedContext ctx)
        => new ClockEmbedHost(def, ctx);

    public IReadOnlyDictionary<string, EmbedPropSchema> GetPropsSchema()
        => new Dictionary<string, EmbedPropSchema>
        {
            ["Format"] = new EmbedPropSchema("Format", EmbedPropType.String, "HH:mm:ss", "时间显示格式", null, null, null, null),
            ["ShowSeconds"] = new EmbedPropSchema("ShowSeconds", EmbedPropType.Bool, true, "显示秒", null, null, null, null)
        };

    public string GetMetaSchema() => "{}";

    public void Dispose() { }
}

internal sealed class ClockEmbedHost : IEmbedHost
{
    private readonly object _gate = new();
    private string _format = "HH:mm:ss";
    private DateTime _now = DateTime.Now;

    public ClockEmbedHost(EmbedDefinition definition, EmbedContext context)
    {
        Definition = definition;
        Context = context;
    }

    public EmbedDefinition Definition { get; }
    public EmbedContext Context { get; }

    public void OnFrame(FrameContext frame)
    {
        lock (_gate) _now = DateTime.Now;
    }

    public void SetProps(IReadOnlyDictionary<string, object> props)
    {
        if (props.TryGetValue("Format", out var f) && f is string fmt) _format = fmt;
    }

    public void SetMeta(JsonNode meta) { }

    public void Render(IVisualRoot root, RenderContext ctx, IRenderContext rc)
    {
        var fields = Definition.Fields;
        double x = GetDouble(fields, "X", 0);
        double y = GetDouble(fields, "Y", 0);
        double w = GetDouble(fields, "W", 200);
        double h = GetDouble(fields, "H", 60);

        rc.DrawRoundedRect(new Rect(x, y, w, h), CornerRadius.Uniform(4),
            new ArgbColor(0xFF202020));

        string text;
        lock (_gate) text = _now.ToString(_format);
        rc.DrawText(text, new Point(x + 4, y + 4), ArgbColor.White,
            "Consolas", 28, FontWeight.Medium);
    }

    private static double GetDouble(IReadOnlyDictionary<string, string> fields, string key, double fallback)
    {
        if (fields.TryGetValue(key, out var v) && double.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        return fallback;
    }

    public HitTestResult HitTest(Point point)
        => new(false, null, HitTestAction.None, point);

    public bool OnInput(InputEvent evt) => false;

    public EmbedStateSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var snapshot = new EmbedStateSnapshot(
                Definition.Name,
                new Dictionary<string, object> { ["Format"] = _format },
                JsonNode.Parse("{}"));
            _now = DateTime.Now;
            return snapshot;
        }
    }

    public string CurrentTime
    {
        get { lock (_gate) { return _now.ToString(_format); } }
    }

    public void Dispose() { }
}
