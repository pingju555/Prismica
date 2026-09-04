using System.Collections.Generic;
using System.Linq;

namespace Prismica.Core.Parsing;

/// <summary>
/// 单个 [Interface.*] 参数的可编辑模型（Schema 设计器用）。
/// Type 取值与 parser 对齐：Text / Number / Color / Font / Bool / Select / Slider / Url。
/// </summary>
public sealed record InterfaceParamEdit(
    string Name,
    string Type,
    string Default,
    string Label,
    string? Min,
    string? Max,
    string? Options)
{
    public static readonly IReadOnlyList<string> KnownTypes =
        new[] { "Text", "Number", "Color", "Font", "Bool", "Select", "Slider", "Url" };
}

/// <summary>
/// [Interface.*] 参数 Schema 的解析与序列化（纯逻辑，可单测）。
/// 仅处理以 "Interface." 开头的参数段；遗留的裸 "Interface" 简写段原样保留。
/// </summary>
public static class InterfaceSchemaSerializer
{
    /// <summary>从 .pri 文本提取所有 [Interface.&lt;name&gt;] 参数定义。</summary>
    public static IReadOnlyList<InterfaceParamEdit> Extract(string priText)
    {
        var lines = priText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = new List<InterfaceParamEdit>();
        string? current = null;
        var fields = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        void Flush()
        {
            if (current is not null && current.StartsWith("Interface.", System.StringComparison.OrdinalIgnoreCase))
            {
                var name = current["Interface.".Length..];
                fields.TryGetValue("Type", out var type);
                fields.TryGetValue("Default", out var def);
                fields.TryGetValue("Label", out var label);
                fields.TryGetValue("Min", out var min);
                fields.TryGetValue("Max", out var max);
                fields.TryGetValue("Options", out var opts);
                result.Add(new InterfaceParamEdit(
                    name,
                    string.IsNullOrWhiteSpace(type) ? "Text" : type,
                    def ?? "",
                    label ?? "",
                    string.IsNullOrWhiteSpace(min) ? null : min,
                    string.IsNullOrWhiteSpace(max) ? null : max,
                    string.IsNullOrWhiteSpace(opts) ? null : opts));
            }
            fields = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        }

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                Flush();
                current = trimmed[1..^1].Trim();
            }
            else if (current is not null && !string.IsNullOrEmpty(trimmed)
                     && !trimmed.StartsWith(';') && !trimmed.StartsWith('#'))
            {
                var eq = trimmed.IndexOf('=');
                if (eq >= 0)
                    fields[trimmed[..eq].Trim()] = trimmed[(eq + 1)..].Trim();
            }
        }
        Flush();
        return result;
    }

    /// <summary>把参数列表写回 .pri 文本：移除旧的 [Interface.*] 段，追加新的，其余原样保留。</summary>
    public static string Apply(string priText, IReadOnlyList<InterfaceParamEdit> edits)
    {
        var lines = priText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var outLines = new List<string>();
        bool inInterfaceSection = false;

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                var name = trimmed[1..^1].Trim();
                inInterfaceSection = name.StartsWith("Interface.", System.StringComparison.OrdinalIgnoreCase);
                if (!inInterfaceSection) outLines.Add(raw);
            }
            else if (!inInterfaceSection)
            {
                outLines.Add(raw);
            }
        }

        outLines.Add("");
        foreach (var e in edits.Where(e => !string.IsNullOrWhiteSpace(e.Name)))
        {
            outLines.Add($"[Interface.{e.Name}]");
            outLines.Add($"Type={e.Type}");
            outLines.Add($"Default={e.Default ?? ""}");
            if (!string.IsNullOrWhiteSpace(e.Label)) outLines.Add($"Label={e.Label}");
            if (!string.IsNullOrWhiteSpace(e.Min)) outLines.Add($"Min={e.Min}");
            if (!string.IsNullOrWhiteSpace(e.Max)) outLines.Add($"Max={e.Max}");
            if (!string.IsNullOrWhiteSpace(e.Options)) outLines.Add($"Options={e.Options}");
            outLines.Add("");
        }

        return string.Join("\n", outLines).Trim('\n') + "\n";
    }
}
