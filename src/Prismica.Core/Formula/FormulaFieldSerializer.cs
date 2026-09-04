using System.Collections.Generic;
using System.Linq;

namespace Prismica.Core.Formula;

/// <summary>[Measure*] 段内 Formula= 公式字段（公式编辑器模型）。</summary>
public sealed record FormulaField(string Section, string Formula);

/// <summary>
/// [Measure*] 段内 Formula= 公式字段的解析与回写（纯逻辑，可单测）。
/// 仅处理 Formula= 行；其余 .pri 内容原样保留。
/// </summary>
public static class FormulaFieldSerializer
{
    /// <summary>提取 .pri 中所有 [Measure*] 段的 Formula= 值，按出现顺序返回。</summary>
    public static IReadOnlyList<FormulaField> Extract(string priText)
    {
        var lines = priText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = new List<FormulaField>();
        string? current = null;
        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                current = trimmed[1..^1].Trim();
            }
            else if (current is not null && !string.IsNullOrEmpty(trimmed)
                     && !trimmed.StartsWith(';') && !trimmed.StartsWith('#'))
            {
                var eq = trimmed.IndexOf('=');
                if (eq >= 0 && string.Equals(trimmed[..eq].Trim(), "Formula", System.StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new FormulaField(current, trimmed[(eq + 1)..].Trim()));
                }
            }
        }
        return result;
    }

    /// <summary>把字段列表写回 .pri：替换每段首个 Formula= 行（缺失则补在段尾），其余原样保留。</summary>
    public static string Apply(string priText, IReadOnlyList<FormulaField> fields)
    {
        var lines = priText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var bySection = fields
            .GroupBy(f => f.Section, System.StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Formula, System.StringComparer.OrdinalIgnoreCase);

        var outLines = new List<string>();
        string? current = null;
        bool replacedForCurrent = false;

        void FlushPendingReplace()
        {
            if (current is not null && bySection.TryGetValue(current, out var newVal) && !replacedForCurrent)
            {
                outLines.Add($"Formula={newVal}");
                replacedForCurrent = true;
            }
        }

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                FlushPendingReplace();
                current = trimmed[1..^1].Trim();
                replacedForCurrent = false;
                outLines.Add(raw);
            }
            else if (current is not null
                     && bySection.TryGetValue(current, out var newVal)
                     && !replacedForCurrent
                     && trimmed.StartsWith("Formula=", System.StringComparison.OrdinalIgnoreCase))
            {
                outLines.Add($"Formula={newVal}");
                replacedForCurrent = true;
            }
            else
            {
                outLines.Add(raw);
            }
        }
        FlushPendingReplace();

        return string.Join("\n", outLines);
    }
}
