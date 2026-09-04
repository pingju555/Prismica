using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Prismica.Core.Components;
using Prismica.Core.Parsing;

namespace Prismica.Core.Theming;

/// <summary>
/// 组件级主题解析器：在 <c>.pri</c> 文本解析前，把活动主题的 <c>@Theme.Key</c> 令牌
/// 替换为具体值。主题段（<c>[Theme.*]</c>）与 <c>[Prismica]</c> 段本身不被替换，保持可编辑、幂等。
/// </summary>
public static class ThemeResolver
{
    /// <summary>匹配 <c>@Theme.Key</c> 令牌。</summary>
    public static readonly Regex TokenRegex =
        new("@Theme\\.(?<k>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    private static readonly Regex SectionRegex =
        new(@"^\s*\[(?<name>[^\]]+)\]\s*$", RegexOptions.Compiled);

    /// <summary>
    /// 解析前替换令牌。
    /// </summary>
    /// <param name="priText">原始 <c>.pri</c> 文本（含 <c>@Theme.X</c> 与 <c>[Theme.*]</c>）。</param>
    /// <param name="overrideName">覆盖活动主题名；为空则用 <c>[Prismica] Theme=</c>。</param>
    /// <param name="diags">可选，写入未知令牌 / 活动主题缺失等诊断。</param>
    /// <param name="filePath">诊断来源路径。</param>
    /// <returns>已替换令牌的文本；无可替换或无活动主题时原样返回。</returns>
    public static string Resolve(
        string priText,
        string? overrideName = null,
        List<Diagnostic>? diags = null,
        string filePath = "<memory>")
    {
        var active = overrideName ?? ThemeCatalog.ExtractActiveName(priText);
        if (active is null)
            return priText;

        var themes = ThemeCatalog.ExtractThemes(priText);
        var theme = themes.FirstOrDefault(t => string.Equals(t.Name, active, StringComparison.OrdinalIgnoreCase));
        if (theme is null)
        {
            diags?.Add(new Diagnostic(DiagnosticSeverity.Warning,
                $"活动主题 '{active}' 未定义，跳过主题替换", filePath, 0, 0, 0, "THEME_ACTIVE_MISSING"));
            return priText;
        }

        var tokens = theme.Tokens;
        var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outLines = new List<string>();

        bool inThemeSection = false, inPrismica = false;
        foreach (var raw in priText.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            var m = SectionRegex.Match(line);
            if (m.Success)
            {
                inThemeSection = m.Groups["name"].Value.StartsWith("Theme", StringComparison.OrdinalIgnoreCase);
                inPrismica = string.Equals(m.Groups["name"].Value, "Prismica", StringComparison.OrdinalIgnoreCase);
                outLines.Add(raw);
                continue;
            }

            if (inThemeSection || inPrismica)
            {
                outLines.Add(raw);
                continue;
            }

            var replaced = TokenRegex.Replace(raw, mr =>
            {
                var key = mr.Groups["k"].Value;
                if (tokens.TryGetValue(key, out var val)) return val;
                unknown.Add(key);
                return mr.Value;
            });
            outLines.Add(replaced);
        }

        foreach (var k in unknown)
            diags?.Add(new Diagnostic(DiagnosticSeverity.Warning,
                $"未知主题令牌: @Theme.{k}", filePath, 0, 0, 0, "THEME_UNKNOWN_TOKEN"));

        return string.Join("\n", outLines);
    }
}
