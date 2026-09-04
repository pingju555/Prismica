using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Prismica.Core.Components;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Prismica.Core.Scheduling;

namespace Prismica.Infra.Embeds;

/// <summary>
/// 音乐控制 Embed：SMTC 媒体会话，播放/暂停/上下曲。
/// </summary>
public sealed class MusicControlEmbedComponent : IEmbedComponent
{
    public string Keyword => "MusicControl";
    public EmbedCapabilities Capabilities => EmbedCapabilities.Animatable;
    public Size DefaultSize => new(300, 80);

    public IEmbedHost CreateHost(EmbedDefinition def, EmbedContext ctx)
        => new MusicControlEmbedHost(def, ctx);

    public IReadOnlyDictionary<string, EmbedPropSchema> GetPropsSchema()
        => new Dictionary<string, EmbedPropSchema>
        {
            ["ShowArtwork"] = new EmbedPropSchema("ShowArtwork", EmbedPropType.Bool, true, "显示封面", null, null, null, null)
        };

    public string GetMetaSchema() => "{}";

    public void Dispose() { }
}

internal sealed class MusicControlEmbedHost : IEmbedHost
{
    private readonly object _gate = new();
    private string _title = "No Media";
    private string _artist = "";
    private string _album = "";
    private bool _isPlaying = false;
    private bool _showArtwork;

    public MusicControlEmbedHost(EmbedDefinition definition, EmbedContext context)
    {
        Definition = definition;
        Context = context;
    }

    public EmbedDefinition Definition { get; }
    public EmbedContext Context { get; }

    public void OnFrame(FrameContext frame)
    {
        // SMTC 事件驱动，不需要轮询
    }

    public void SetProps(IReadOnlyDictionary<string, object> props)
    {
        if (props.TryGetValue("ShowArtwork", out var a) && a is bool show) _showArtwork = show;
    }

    public void SetMeta(JsonNode meta) { }

    public void Render(IVisualRoot root, RenderContext ctx, IRenderContext rc)
    {
        var fields = Definition.Fields;
        double x = GetDouble(fields, "X", 0);
        double y = GetDouble(fields, "Y", 0);
        double w = GetDouble(fields, "W", 300);
        double h = GetDouble(fields, "H", 80);

        rc.DrawRoundedRect(new Rect(x, y, w, h), CornerRadius.Uniform(8),
            new ArgbColor(0xFF2D2D2D));

        string title, artist, album;
        bool playing;
        lock (_gate)
        {
            title = _title;
            artist = _artist;
            album = _album;
            playing = _isPlaying;
        }

        rc.DrawText(title, new Point(x + 10, y + 10), ArgbColor.White,
            "Segoe UI", 16, FontWeight.Bold);
        rc.DrawText($"{artist} - {album}", new Point(x + 10, y + 35),
            new ArgbColor(0xFFB0B0B0), "Microsoft YaHei", 12, FontWeight.Normal);

        // 播放/暂停按钮
        string btnText = playing ? "⏸" : "▶";
        rc.DrawText(btnText, new Point(x + w - 50, y + h - 35), ArgbColor.White,
            "Segoe UI", 20, FontWeight.Bold);
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
                    ["Title"] = _title,
                    ["Artist"] = _artist,
                    ["Album"] = _album,
                    ["IsPlaying"] = _isPlaying
                },
                JsonNode.Parse("{}"));
        }
    }

    public void Dispose() { }
}
