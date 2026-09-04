using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Prismica.Core.Components;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Prismica.Core.Scheduling;

namespace Prismica.Infra.Embeds;

/// <summary>
/// 便签 Embed：本地文本便签，支持拖拽编辑。
/// </summary>
public sealed class StickyNoteEmbedComponent : IEmbedComponent
{
    public string Keyword => "StickyNote";
    public EmbedCapabilities Capabilities => EmbedCapabilities.Animatable;
    public Size DefaultSize => new(200, 150);

    public IEmbedHost CreateHost(EmbedDefinition def, EmbedContext ctx)
        => new StickyNoteEmbedHost(def, ctx);

    public IReadOnlyDictionary<string, EmbedPropSchema> GetPropsSchema()
        => new Dictionary<string, EmbedPropSchema>
        {
            ["Text"] = new EmbedPropSchema("Text", EmbedPropType.String, "便签内容", "便签文本", null, null, null, null),
            ["FontSize"] = new EmbedPropSchema("FontSize", EmbedPropType.Number, 14.0, "字体大小", null, null, null, null),
            ["BgColor"] = new EmbedPropSchema("BgColor", EmbedPropType.Color, "#FFE8B4", "背景颜色", null, null, null, null)
        };

    public string GetMetaSchema() => "{}";

    public void Dispose() { }
}

internal sealed class StickyNoteEmbedHost : IEmbedHost
{
    private readonly object _gate = new();
    private string _text = "便签内容";
    private double _fontSize = 14;
    private string _bgColor = "#FFE8B4";
    private readonly string _persistPath;

    public StickyNoteEmbedHost(EmbedDefinition definition, EmbedContext context)
    {
        Definition = definition;
        Context = context;
        _persistPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Prismica",
            $"sticky-{definition.Name}.txt");
        Load();
    }

    public EmbedDefinition Definition { get; }
    public EmbedContext Context { get; }

    public void OnFrame(FrameContext frame) { }

    public void SetProps(IReadOnlyDictionary<string, object> props)
    {
        lock (_gate)
        {
            if (props.TryGetValue("Text", out var t) && t is string txt) _text = txt;
            if (props.TryGetValue("FontSize", out var fs) && fs is double sz) _fontSize = sz;
            if (props.TryGetValue("BgColor", out var c) && c is string color) _bgColor = color;
        }
        Save();
    }

    public void SetMeta(JsonNode meta) { }

    public void Render(IVisualRoot root, RenderContext ctx, IRenderContext rc)
    {
        var fields = Definition.Fields;
        double x = GetDouble(fields, "X", 0);
        double y = GetDouble(fields, "Y", 0);
        double w = GetDouble(fields, "W", 200);
        double h = GetDouble(fields, "H", 150);

        var bgColor = ParseColor(_bgColor, 0xFFE8B4);
        rc.DrawRoundedRect(new Rect(x, y, w, h), CornerRadius.Uniform(6), bgColor);

        string text;
        lock (_gate) text = _text;
        rc.DrawText(text, new Point(x + 8, y + 8), new ArgbColor(0xFF333333),
            "Microsoft YaHei", (float)_fontSize, FontWeight.Normal);
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_persistPath))
            {
                var content = File.ReadAllText(_persistPath);
                lock (_gate) _text = content;
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_persistPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            lock (_gate) File.WriteAllText(_persistPath, _text);
        }
        catch { }
    }

    private static double GetDouble(IReadOnlyDictionary<string, string> fields, string key, double fallback)
    {
        if (fields.TryGetValue(key, out var v) && double.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        return fallback;
    }

    private static ArgbColor ParseColor(string hex, uint fallback)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6) hex = "FF" + hex;
            if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var argb))
                return new ArgbColor(argb);
        }
        catch { }
        return new ArgbColor(fallback);
    }

    public HitTestResult HitTest(Point point)
        => new(false, null, HitTestAction.None, point);

    public bool OnInput(InputEvent evt) => false;

    public EmbedStateSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new EmbedStateSnapshot(
                Definition.Name,
                new Dictionary<string, object>
                {
                    ["Text"] = _text,
                    ["FontSize"] = _fontSize,
                    ["BgColor"] = _bgColor
                },
                JsonNode.Parse("{}"));
        }
    }

    public void Dispose() { }
}
