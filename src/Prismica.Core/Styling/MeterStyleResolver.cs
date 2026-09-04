using System;
using System.Collections.Generic;
using System.Linq;
using Prismica.Core.Components;

namespace Prismica.Core.Styling;

/// <summary>
/// 解析结果：合并后的字段、直接引用的父样式名（有序）、未找到的样式名。
/// </summary>
public sealed record StyleResolutionResult(
    IReadOnlyDictionary<string, string> MergedFields,
    IReadOnlyList<string> ParentStyles,
    IReadOnlyList<string> MissingStyles
);

/// <summary>
/// MeterStyle 继承解析（纯逻辑，可单测）。
/// <para>
/// 早期架构缺口「MeterStyle 继承」：<c>.pri</c> 可用 <c>[MeterStyle*]</c> / <c>[Style*]</c> 段定义命名样式，
/// meter 段用 <c>MeterStyle=Name1,Name2</c>（或 <c>Style=</c>）引用；解析时把引用样式的字段合并进 meter 自身字段，
/// 合并优先级：样式按引用顺序排列（后者覆盖前者）&lt; meter 自身字段（最高）。
/// 样式自身也可含 <c>MeterStyle=</c> 引用其它样式（嵌套继承），带环检测避免死循环。
/// 键大小写不敏感；引用键（MeterStyle/Style）不会进入最终合并字段。
/// </para>
/// </summary>
public static class MeterStyleResolver
{
    /// <summary>
    /// 合并 meter 字段与其引用的命名样式。
    /// </summary>
    /// <param name="meterFields">meter 段的原始字段（含可能的 MeterStyle/Style 引用键）。</param>
    /// <param name="styles">组件内所有 <see cref="StyleDefinition"/>。</param>
    public static StyleResolutionResult Resolve(
        IReadOnlyDictionary<string, string> meterFields,
        IReadOnlyList<StyleDefinition> styles)
    {
        var byName = styles.ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parentStyles = new List<string>();
        var missing = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? refRaw = TryGetRef(meterFields, "MeterStyle") ?? TryGetRef(meterFields, "Style");
        if (!string.IsNullOrWhiteSpace(refRaw))
        {
            foreach (var name in SplitRef(refRaw!))
                MergeStyle(name, byName, merged, parentStyles, missing, visited);
        }

        // 叠加 meter 自身字段（排除引用键）。
        foreach (var (k, v) in meterFields)
        {
            if (string.Equals(k, "MeterStyle", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(k, "Style", StringComparison.OrdinalIgnoreCase)) continue;
            merged[k] = v;
        }

        return new StyleResolutionResult(merged, parentStyles, missing);
    }

    private static void MergeStyle(
        string name,
        Dictionary<string, StyleDefinition> byName,
        Dictionary<string, string> merged,
        List<string> parentStyles,
        List<string> missing,
        HashSet<string> visited)
    {
        if (!byName.TryGetValue(name, out var style))
        {
            if (visited.Add(name)) missing.Add(name); // 仅首次记录缺失，避免重复
            return;
        }
        if (!visited.Add(name)) return; // 环：已处理过，跳过
        parentStyles.Add(name);

        // 先递归合并该样式引用的父样式（父在前，本样式在后覆盖）。
        if (style.Fields.TryGetValue("MeterStyle", out var raw) && !string.IsNullOrWhiteSpace(raw))
            foreach (var p in SplitRef(raw))
                MergeStyle(p, byName, merged, parentStyles, missing, visited);
        else if (style.Fields.TryGetValue("Style", out var raw2) && !string.IsNullOrWhiteSpace(raw2))
            foreach (var p in SplitRef(raw2))
                MergeStyle(p, byName, merged, parentStyles, missing, visited);

        // 再叠加该样式自身字段（排除引用键）。
        foreach (var (k, v) in style.Fields)
        {
            if (string.Equals(k, "MeterStyle", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(k, "Style", StringComparison.OrdinalIgnoreCase)) continue;
            merged[k] = v;
        }
    }

    private static string? TryGetRef(IReadOnlyDictionary<string, string> fields, string key)
        => fields.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    private static IEnumerable<string> SplitRef(string raw)
        => raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
