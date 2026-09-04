using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Prismica.Core.Components;
using Prismica.Core.Parsing;

namespace Prismica.Core.Theming;

/// <summary>
/// 组件级主题目录：解析 / 序列化 / 校验 <c>.pri</c> 中的 <c>[Theme.*]</c> 主题段与
/// <c>[Prismica] Theme=</c> 选择。纯文本操作，可被单元测试覆盖。
/// </summary>
public static class ThemeCatalog
{
    private static readonly Regex SectionRegex =
        new(@"^\s*\[(?<name>[^\]]+)\]\s*$", RegexOptions.Compiled);
    private static readonly Regex KvRegex =
        new(@"^\s*(?<k>[^=;#]+?)\s*=\s*(?<v>.*?)\s*$", RegexOptions.Compiled);

    /// <summary>解析所有 <c>[Theme.&lt;Name&gt;]</c> 段为令牌集合。</summary>
    public static IReadOnlyList<ThemeSpec> ExtractThemes(string priText)
    {
        var result = new List<ThemeSpec>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in SplitSections(priText))
        {
            if (!section.Name.StartsWith("Theme", StringComparison.OrdinalIgnoreCase)) continue;
            var raw = section.Name["Theme".Length..].Trim();
            var name = raw.StartsWith(".") ? raw[1..].Trim() : raw;
            if (name.Length == 0) continue;

            var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in section.Kv)
                tokens[k] = v;

            if (seen.Add(name))
                result.Add(new ThemeSpec(name, tokens));
        }
        return result;
    }

    /// <summary>读取 <c>[Prismica] Theme=</c> 选中的活动主题名（无则返回 null）。多个 <c>[Prismica]</c> 段时取最后一个含 Theme 的。</summary>
    public static string? ExtractActiveName(string priText)
    {
        string? found = null;
        foreach (var section in SplitSections(priText))
        {
            if (!string.Equals(section.Name, "Prismica", StringComparison.OrdinalIgnoreCase)) continue;
            if (section.Kv.TryGetValue("Theme", out var v) && !string.IsNullOrWhiteSpace(v))
                found = v.Trim();
        }
        return found;
    }

    /// <summary>
    /// 把主题集合与活动选择写回 <c>.pri</c> 文本：先剔除旧 <c>[Theme.*]</c> 段与旧 <c>Theme=</c> 行，
    /// 再在文末追加主题段、在文末补 <c>[Prismica] Theme=</c>。其余内容原样保留。
    /// </summary>
    public static string Apply(string priText, IReadOnlyList<ThemeSpec> themes, string? active)
    {
        var kept = new List<string>();
        bool inThemeSection = false;
        bool inPrismica = false;

        foreach (var raw in priText.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            var m = SectionRegex.Match(line);
            if (m.Success)
            {
                inThemeSection = m.Groups["name"].Value.StartsWith("Theme", StringComparison.OrdinalIgnoreCase);
                inPrismica = string.Equals(m.Groups["name"].Value, "Prismica", StringComparison.OrdinalIgnoreCase);
                if (inThemeSection) continue; // 丢弃整个旧主题段
                kept.Add(raw);
                continue;
            }

            if (inThemeSection) continue;
            if (inPrismica)
            {
                var km = KvRegex.Match(line);
                if (km.Success && string.Equals(km.Groups["k"].Value.Trim(), "Theme", StringComparison.OrdinalIgnoreCase))
                    continue; // 丢弃旧 Theme= 行
            }
            kept.Add(raw);
        }

        var sb = new System.Text.StringBuilder();
        foreach (var l in kept) sb.AppendLine(l);
        sb.AppendLine();

        foreach (var t in themes)
        {
            sb.AppendLine($"[Theme.{t.Name}]");
            foreach (var (k, v) in t.Tokens)
                sb.AppendLine($"{k}={v}");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(active))
        {
            sb.AppendLine("[Prismica]");
            sb.AppendLine($"Theme={active}");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>校验主题：重复名（错误）、活动主题未定义（警告）、未知令牌引用（警告）。</summary>
    public static IReadOnlyList<Diagnostic> Validate(string priText, string filePath = "<memory>")
    {
        var diags = new List<Diagnostic>();

        // 重复主题名：直接扫描原始段（ExtractThemes 会去重，故不能依赖它）
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in SplitSections(priText))
        {
            if (!section.Name.StartsWith("Theme", StringComparison.OrdinalIgnoreCase)) continue;
            var raw = section.Name["Theme".Length..].Trim();
            var nm = raw.StartsWith(".") ? raw[1..].Trim() : raw;
            if (nm.Length == 0) continue;
            if (!seenNames.Add(nm))
                diags.Add(new Diagnostic(DiagnosticSeverity.Error,
                    $"重复的主题名: {nm}", filePath, 0, 0, 0, "THEME_DUP"));
        }

        var themes = ExtractThemes(priText);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in themes) names.Add(t.Name);

        var active = ExtractActiveName(priText);
        if (active is not null && !names.Contains(active))
            diags.Add(new Diagnostic(DiagnosticSeverity.Warning,
                $"活动主题 '{active}' 未在 [Theme.*] 中定义", filePath, 0, 0, 0, "THEME_ACTIVE_MISSING"));

        var knownSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var activeTheme = themes.FirstOrDefault(t => string.Equals(t.Name, active, StringComparison.OrdinalIgnoreCase));
        if (activeTheme is not null)
            foreach (var k in activeTheme.Tokens.Keys) knownSet.Add(k);

        var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool inThemeSection = false, inPrismica = false;
        foreach (var raw in priText.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            var m = SectionRegex.Match(line);
            if (m.Success)
            {
                inThemeSection = m.Groups["name"].Value.StartsWith("Theme", StringComparison.OrdinalIgnoreCase);
                inPrismica = string.Equals(m.Groups["name"].Value, "Prismica", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (inThemeSection || inPrismica) continue;

            foreach (Match r in ThemeResolver.TokenRegex.Matches(line))
            {
                var key = r.Groups["k"].Value;
                if (!knownSet.Contains(key)) unknown.Add(key);
            }
        }
        foreach (var k in unknown)
            diags.Add(new Diagnostic(DiagnosticSeverity.Warning,
                $"未知主题令牌: @Theme.{k}", filePath, 0, 0, 0, "THEME_UNKNOWN_TOKEN"));

        return diags;
    }

    private static IEnumerable<(string Name, Dictionary<string, string> Kv)> SplitSections(string priText)
    {
        string? cur = null;
        var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in priText.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            var m = SectionRegex.Match(line);
            if (m.Success)
            {
                if (cur is not null) yield return (cur, kv);
                cur = m.Groups["name"].Value.Trim();
                kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else if (cur is not null)
            {
                if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#')) continue;
                var km = KvRegex.Match(line);
                if (km.Success) kv[km.Groups["k"].Value.Trim()] = km.Groups["v"].Value;
            }
        }
        if (cur is not null) yield return (cur, kv);
    }
}
