using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Prismica.Core.Components;
using Prismica.Core.Native;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Prismica.Core.Scheduling;
using Prismica.Infra.Wpf;

namespace Prismica.Infra.Embeds;

/// <summary>
/// 图标格子 Embed：把桌面（用户/公共）的文件与文件夹渲染成可点击的网格。
/// 依赖 INativeDesktop（枚举条目 + 提取图标 + 打开），无 Native 时优雅降级为空白网格。
/// </summary>
public sealed class IconGridEmbedComponent : IEmbedComponent
{
    public string Keyword => "IconGrid";
    public EmbedCapabilities Capabilities => EmbedCapabilities.Interactive | EmbedCapabilities.ShellIntegration | EmbedCapabilities.FileAccess;
    public Size DefaultSize => new(320, 320);

    public IEmbedHost CreateHost(EmbedDefinition def, EmbedContext ctx)
        => new IconGridEmbedHost(def, ctx);

    public IReadOnlyDictionary<string, EmbedPropSchema> GetPropsSchema()
        => new Dictionary<string, EmbedPropSchema>
        {
            ["Columns"] = new EmbedPropSchema("Columns", EmbedPropType.Number, 4.0, "列数", 1, 12, 1, null),
            ["Spacing"] = new EmbedPropSchema("Spacing", EmbedPropType.Number, 8.0, "单元格间距", 0, 32, 1, null),
            ["ShowLabels"] = new EmbedPropSchema("ShowLabels", EmbedPropType.Bool, true, "显示名称", null, null, null, null),
            ["SortBy"] = new EmbedPropSchema("SortBy", EmbedPropType.Select, "Name", "排序方式", null, null, null, new[] { "Name", "Type" }),
            ["IconSize"] = new EmbedPropSchema("IconSize", EmbedPropType.Select, "Large", "图标尺寸", null, null, null, new[] { "Small", "Large", "ExtraLarge" }),
            ["Source"] = new EmbedPropSchema("Source", EmbedPropType.Select, "All", "图标来源", null, null, null, new[] { "All", "User", "Common" }),
        };

    public string GetMetaSchema() => "{}";

    public void Dispose() { }
}

internal sealed class IconGridEmbedHost : IEmbedHost
{
    private readonly object _gate = new();
    private readonly List<(Rect cell, string path)> _cells = new();
    private readonly Dictionary<string, IImage> _icons = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<DesktopIconItem> _allItems = Array.Empty<DesktopIconItem>();
    private IReadOnlyList<DesktopIconItem> _items = Array.Empty<DesktopIconItem>();
    private bool _loaded;

    private double _columns = 4;
    private double _spacing = 8;
    private bool _showLabels = true;
    private string _sortBy = "Name";
    private string _source = "All";
    private IconSize _iconSize = IconSize.Large;

    public IconGridEmbedHost(EmbedDefinition definition, EmbedContext context)
    {
        Definition = definition;
        Context = context;
        ApplyConfig(definition.Fields);
        if (Context.Native is not null)
            _ = LoadAsync();
    }

    public EmbedDefinition Definition { get; }
    public EmbedContext Context { get; }

    public void OnFrame(FrameContext frame) { }

    public void SetProps(IReadOnlyDictionary<string, object> props)
    {
        if (props.TryGetValue("Columns", out var c) && c is double cd) _columns = cd;
        if (props.TryGetValue("Spacing", out var sp) && sp is double sd) _spacing = sd;
        if (props.TryGetValue("ShowLabels", out var sl) && sl is bool b) _showLabels = b;
        if (props.TryGetValue("SortBy", out var sb) && sb is string s2) _sortBy = s2;
        if (props.TryGetValue("IconSize", out var isz) && isz is string i2) _iconSize = ParseIconSize(i2);
        if (props.TryGetValue("Source", out var so) && so is string s3) _source = s3;
        RebuildItems();
    }

    public void SetMeta(JsonNode meta) { }

    public void Render(IVisualRoot root, RenderContext ctx, IRenderContext rc)
    {
        var fields = Definition.Fields;
        double x = GetDouble(fields, "X", 0);
        double y = GetDouble(fields, "Y", 0);
        double w = GetDouble(fields, "W", 320);
        double h = GetDouble(fields, "H", 320);

        IReadOnlyList<DesktopIconItem> items;
        Dictionary<string, IImage> icons;
        lock (_gate)
        {
            items = _items;
            icons = new Dictionary<string, IImage>(_icons, StringComparer.OrdinalIgnoreCase);
        }

        int cols = _columns <= 0 ? 1 : (int)Math.Round(_columns);
        double spacing = _spacing;
        double labelH = _showLabels ? 18 : 0;
        double cellW = cols > 0 ? (w - spacing * (cols + 1)) / cols : w;
        double iconBox = Math.Max(8, cellW - 8);
        double cellH = iconBox + labelH + 4;

        _cells.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            int r = i / cols;
            int c = i % cols;
            double cx = x + spacing + c * (cellW + spacing);
            double cy = y + spacing + r * (cellH + spacing);
            var item = items[i];
            _cells.Add((new Rect(cx, cy, cellW, cellH), item.Path));

            double iconSize = Math.Min(iconBox, cellH - labelH);
            double ix = cx + (cellW - iconSize) / 2;
            double iy = cy;

            if (icons.TryGetValue(item.Path, out var img) && img is not null)
            {
                rc.DrawImage(img, new Rect(ix, iy, iconSize, iconSize));
            }
            else
            {
                rc.DrawRoundedRect(new Rect(ix, iy, iconSize, iconSize), CornerRadius.Uniform(4),
                    item.IsFolder ? new ArgbColor(0xFF3A6EA5) : new ArgbColor(0xFF555555));
            }

            if (_showLabels)
            {
                string label = item.Name.Length > 14 ? item.Name[..14] + "…" : item.Name;
                rc.DrawText(label, new Point(cx + 2, cy + iconSize + 2), ArgbColor.White,
                    "Microsoft YaHei", 11, FontWeight.Normal);
            }
        }
    }

    public HitTestResult HitTest(Point point)
    {
        string? hit = CellAt(point);
        return hit is null
            ? new HitTestResult(false, null, HitTestAction.None, point)
            : new HitTestResult(true, hit, HitTestAction.DoubleClick, point);
    }

    public bool OnInput(InputEvent evt)
    {
        if (evt.Type == InputType.MouseDown && evt.Button == MouseButton.Left)
        {
            string? path = CellAt(evt.Position);
            if (path is not null)
            {
                _ = Context.Native?.ExecuteVerbAsync(path, "open", IntPtr.Zero);
                return true;
            }
        }
        return false;
    }

    public EmbedStateSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new EmbedStateSnapshot(
                Definition.Name,
                new Dictionary<string, object>
                {
                    ["Count"] = _items.Count,
                    ["Loaded"] = _loaded
                },
                JsonNode.Parse("{}"));
        }
    }

    public void Dispose() { }

    private async Task LoadAsync()
    {
        var native = Context.Native;
        if (native is null) return;
        try
        {
            var all = native.GetDesktopIcons();
            lock (_gate) _allItems = all;
            RebuildItems();

            foreach (var it in all)
            {
                try
                {
                    var icon = await native.GetIconAsync(it.Path, _iconSize, false).ConfigureAwait(false);
                    var img = WpfImageFactory.FromIconData(icon);
                    lock (_gate) _icons[it.Path] = img;
                }
                catch
                {
                    // 单个图标提取失败不影响其余
                }
            }
            lock (_gate) _loaded = true;
        }
        catch
        {
            // Native 不可用：保持空白网格
        }
    }

    private void RebuildItems()
    {
        lock (_gate)
        {
            _items = SortItems(FilterBySource(_allItems, _source), _sortBy);
        }
    }

    private static IReadOnlyList<DesktopIconItem> FilterBySource(IReadOnlyList<DesktopIconItem> items, string source)
    {
        if (source.Equals("All", StringComparison.OrdinalIgnoreCase)) return items;
        string? target = source.Equals("Common", StringComparison.OrdinalIgnoreCase)
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (string.IsNullOrEmpty(target)) return items;
        return items.Where(it => it.Path.StartsWith(target, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static IReadOnlyList<DesktopIconItem> SortItems(IReadOnlyList<DesktopIconItem> items, string sortBy)
    {
        return sortBy.Equals("Type", StringComparison.OrdinalIgnoreCase)
            ? items.OrderBy(it => it.IsFolder ? 0 : 1).ThenBy(it => it.Name, StringComparer.OrdinalIgnoreCase).ToList()
            : items.OrderBy(it => it.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IconSize ParseIconSize(string s) => s.Trim().ToLowerInvariant() switch
    {
        "small" => IconSize.Small,
        "extralarge" => IconSize.ExtraLarge,
        _ => IconSize.Large
    };

    private string? CellAt(Point p)
    {
        foreach (var (cell, path) in _cells)
            if (cell.Contains(p)) return path;
        return null;
    }

    private void ApplyConfig(IReadOnlyDictionary<string, string> fields)
    {
        _columns = GetDouble(fields, "Columns", 4);
        _spacing = GetDouble(fields, "Spacing", 8);
        _showLabels = GetBool(fields, "ShowLabels", true);
        _sortBy = fields.TryGetValue("SortBy", out var s) ? s : "Name";
        _source = fields.TryGetValue("Source", out var src) ? src : "All";
        _iconSize = fields.TryGetValue("IconSize", out var sz) ? ParseIconSize(sz) : IconSize.Large;
    }

    private static double GetDouble(IReadOnlyDictionary<string, string> fields, string key, double fallback)
    {
        if (fields.TryGetValue(key, out var v) && double.TryParse(v, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        return fallback;
    }

    private static bool GetBool(IReadOnlyDictionary<string, string> fields, string key, bool fallback)
    {
        if (fields.TryGetValue(key, out var v))
            return bool.TryParse(v, out var b) ? b : fallback;
        return fallback;
    }
}
