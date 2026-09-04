using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Prismica.Core.Components;
using Prismica.Core.Scheduling;

namespace Prismica.Core.Parsing;

/// <summary>
/// 负责把 .pri 文本中的 <c>[Animation*]</c> 段解析为 <see cref="AnimationSpec"/> 列表，
/// 以及把编辑后的规格序列化回 .pri（其余内容原样保留）。
/// 与 <see cref="InterfaceSchemaSerializer"/> 同构，作为 Studio 动画设计器的纯逻辑后端。
/// </summary>
public static class AnimationSpecSerializer
{
    private const string SectionPrefix = "Animation";

    /// <summary>从 .pri 文本提取所有动画规格。</summary>
    public static IReadOnlyList<AnimationSpec> Extract(string priText)
    {
        var sections = new List<(string Name, Dictionary<string, string> Fields)>();
        Dictionary<string, string>? dict = null;
        string? current = null;

        foreach (var raw in priText.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                if (dict != null && current != null) sections.Add((current, dict));
                current = line[1..^1].Trim();
                dict = current.StartsWith(SectionPrefix, StringComparison.OrdinalIgnoreCase)
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : null;
                continue;
            }
            if (dict == null) continue;
            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            dict[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        if (dict != null && current != null) sections.Add((current, dict));

        var result = new List<AnimationSpec>();
        foreach (var (section, fields) in sections)
        {
            if (!section.StartsWith(SectionPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            var name = section.Length == SectionPrefix.Length ? "" : section[SectionPrefix.Length..];
            result.Add(new AnimationSpec(
                name,
                AnimationSpec.ParseTrigger(fields.GetValueOrDefault("Trigger", "OnShow")),
                fields.GetValueOrDefault("Target", ""),
                AnimationSpec.ParseProperty(fields.GetValueOrDefault("Property", "Opacity")),
                ParseDouble(fields.GetValueOrDefault("From", "0"), 0),
                ParseDouble(fields.GetValueOrDefault("To", "1"), 1),
                ParseInt(fields.GetValueOrDefault("Duration", "300"), 300),
                fields.GetValueOrDefault("Easing", "Linear"),
                ParseBool(fields.GetValueOrDefault("AutoReverse", "False")),
                ParseInt(fields.GetValueOrDefault("Repeat", "0"), 0),
                ParseInt(fields.GetValueOrDefault("Delay", "0"), 0)
            ));
        }
        return result;
    }

    /// <summary>把动画规格写回 .pri，移除旧的 [Animation*] 段后重新追加，其余内容保持不变。</summary>
    public static string Apply(string priText, IReadOnlyList<AnimationSpec> specs)
    {
        var lines = priText.Replace("\r\n", "\n").Split('\n');
        var kept = new List<string>();
        bool inAnim = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var sec = line[1..^1].Trim();
                inAnim = sec.StartsWith(SectionPrefix, StringComparison.OrdinalIgnoreCase);
                if (inAnim) continue;
            }
            if (inAnim) continue;
            kept.Add(raw);
        }

        var sb = new StringBuilder();
        foreach (var l in kept) sb.AppendLine(l);
        if (sb.Length > 0 && !sb.ToString().EndsWith("\n\n")) sb.AppendLine();

        foreach (var s in specs)
        {
            var secName = string.IsNullOrEmpty(s.Name) ? SectionPrefix : SectionPrefix + s.Name;
            sb.AppendLine($"[{secName}]");
            sb.AppendLine($"Trigger={s.Trigger}");
            sb.AppendLine($"Target={s.Target}");
            sb.AppendLine($"Property={s.Property}");
            sb.AppendLine($"From={Num(s.From)}");
            sb.AppendLine($"To={Num(s.To)}");
            sb.AppendLine($"Duration={s.DurationMs}");
            sb.AppendLine($"Easing={s.EasingName}");
            sb.AppendLine($"AutoReverse={s.AutoReverse}");
            sb.AppendLine($"Repeat={s.Repeat}");
            sb.AppendLine($"Delay={s.DelayMs}");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>校验动画规格，输出诊断（未知缓动/负时长/缺 Target 等）。</summary>
    public static IReadOnlyList<Diagnostic> Validate(
        IReadOnlyList<AnimationSpec> specs,
        IReadOnlyList<string>? knownTargets = null)
    {
        knownTargets ??= Array.Empty<string>();
        var diags = new List<Diagnostic>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in specs)
        {
            if (string.IsNullOrWhiteSpace(s.Name))
                diags.Add(Warn("动画缺少名称（节名为 [Animation]，未提供实例名）", "ANIM_NO_NAME"));
            if (string.IsNullOrWhiteSpace(s.Target))
                diags.Add(Err($"动画 '{s.Name}' 缺少 Target（被动画的 meter/measure/embed 名称）", "ANIM_NO_TARGET"));
            else if (!knownTargets.Contains(s.Target, StringComparer.OrdinalIgnoreCase))
                diags.Add(Warn($"动画 '{s.Name}' 的 Target '{s.Target}' 不在已知 meter/measure/embed 名称中", "ANIM_TARGET_UNKNOWN"));
            if (!NamedEasing.TryResolve(s.EasingName, out _))
                diags.Add(Err($"动画 '{s.Name}' 使用了未知缓动 '{s.EasingName}'", "ANIM_BAD_EASING"));
            if (s.DurationMs <= 0)
                diags.Add(Err($"动画 '{s.Name}' 的 Duration 必须 > 0", "ANIM_BAD_DURATION"));
            if (!string.IsNullOrWhiteSpace(s.Name) && !seen.Add(s.Name))
                diags.Add(Warn($"动画名称重复 '{s.Name}'", "ANIM_DUP_NAME"));
        }
        return diags;
    }

    private static Diagnostic Err(string m, string code) =>
        new(DiagnosticSeverity.Error, m, "<animation>", 0, 0, 0, code);
    private static Diagnostic Warn(string m, string code) =>
        new(DiagnosticSeverity.Warning, m, "<animation>", 0, 0, 0, code);

    private static double ParseDouble(string s, double d) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : d;
    private static int ParseInt(string s, int d) =>
        int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : d;
    private static bool ParseBool(string s) =>
        bool.TryParse(s, out var b) && b;
    private static string Num(double d) =>
        d.ToString(CultureInfo.InvariantCulture);
}
