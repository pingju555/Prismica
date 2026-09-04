using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Prismica.Core.Components;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Prismica.Core.Scheduling;

namespace Prismica.Infra.Embeds;

/// <summary>
/// 天气 Embed：调用 wttr.in API 获取当前天气。
/// </summary>
public sealed class WeatherEmbedComponent : IEmbedComponent
{
    public string Keyword => "Weather";
    public EmbedCapabilities Capabilities => EmbedCapabilities.Animatable;
    public Size DefaultSize => new(200, 100);

    public IEmbedHost CreateHost(EmbedDefinition def, EmbedContext ctx)
        => new WeatherEmbedHost(def, ctx);

    public IReadOnlyDictionary<string, EmbedPropSchema> GetPropsSchema()
        => new Dictionary<string, EmbedPropSchema>
        {
            ["City"] = new EmbedPropSchema("City", EmbedPropType.String, "Beijing", "城市名称", null, null, null, null),
            ["Unit"] = new EmbedPropSchema("Unit", EmbedPropType.String, "C", "温度单位 (C/F)", null, null, null, null)
        };

    public string GetMetaSchema() => "{}";

    public void Dispose() { }
}

internal sealed class WeatherEmbedHost : IEmbedHost
{
    private readonly object _gate = new();
    private readonly HttpClient _httpClient = new();
    private string _city = "Beijing";
    private string _unit = "C";
    private string _temperature = "--";
    private string _condition = "Unknown";
    private string _lastUpdate = "";
    private DateTime _nextUpdate = DateTime.MinValue;

    public WeatherEmbedHost(EmbedDefinition definition, EmbedContext context)
    {
        Definition = definition;
        Context = context;
    }

    public EmbedDefinition Definition { get; }
    public EmbedContext Context { get; }

    public void OnFrame(FrameContext frame)
    {
        if (DateTime.Now < _nextUpdate) return;
        _nextUpdate = DateTime.Now.AddMinutes(10);
        _ = FetchWeatherAsync();
    }

    public void SetProps(IReadOnlyDictionary<string, object> props)
    {
        if (props.TryGetValue("City", out var c) && c is string city) _city = city;
        if (props.TryGetValue("Unit", out var u) && u is string unit) _unit = unit;
        _nextUpdate = DateTime.MinValue; // 强制刷新
    }

    public void SetMeta(JsonNode meta) { }

    private async Task FetchWeatherAsync()
    {
        try
        {
            var url = $"https://wttr.in/{Uri.EscapeDataString(_city)}?format=j1";
            var response = await _httpClient.GetStringAsync(url);
            var json = JsonNode.Parse(response);

            if (json?["current_condition"] is JsonArray conditions && conditions.Count > 0)
            {
                var current = conditions[0];
                var tempC = current?["temp_C"]?.ToString() ?? "--";
                var tempF = current?["temp_F"]?.ToString() ?? "--";
                var desc = current?["weatherDesc"]?[0]?["value"]?.ToString() ?? "Unknown";

                lock (_gate)
                {
                    _temperature = _unit.ToUpper() == "F" ? $"{tempF}°F" : $"{tempC}°C";
                    _condition = desc;
                    _lastUpdate = DateTime.Now.ToString("HH:mm");
                }
            }
        }
        catch
        {
            lock (_gate)
            {
                _temperature = "--";
                _condition = "Error";
            }
        }
    }

    public void Render(IVisualRoot root, RenderContext ctx, IRenderContext rc)
    {
        var fields = Definition.Fields;
        double x = GetDouble(fields, "X", 0);
        double y = GetDouble(fields, "Y", 0);
        double w = GetDouble(fields, "W", 200);
        double h = GetDouble(fields, "H", 100);

        rc.DrawRoundedRect(new Rect(x, y, w, h), CornerRadius.Uniform(8),
            new ArgbColor(0xFF1E3A5F));

        string temp, cond, update;
        lock (_gate)
        {
            temp = _temperature;
            cond = _condition;
            update = _lastUpdate;
        }

        rc.DrawText(temp, new Point(x + 10, y + 10), ArgbColor.White,
            "Segoe UI", 32, FontWeight.Bold);
        rc.DrawText(cond, new Point(x + 10, y + 50), new ArgbColor(0xFFB0C4DE),
            "Microsoft YaHei", 14, FontWeight.Normal);

        if (!string.IsNullOrEmpty(update))
        {
            rc.DrawText($"Updated: {update}", new Point(x + 10, y + h - 25),
                new ArgbColor(0xFF808080), "Segoe UI", 10, FontWeight.Normal);
        }
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
            return new EmbedStateSnapshot(
                Definition.Name,
                new Dictionary<string, object>
                {
                    ["City"] = _city,
                    ["Unit"] = _unit,
                    ["Temperature"] = _temperature,
                    ["Condition"] = _condition
                },
                JsonNode.Parse("{}"));
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
