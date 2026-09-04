using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Prismica.Core.Native;
using Prismica.Core.Parsing;

namespace Prismica.Core.MultiScreen;

/// <summary>
/// 多屏差异化配置目录：解析 desktop.profile 文本，按实际屏幕解析每屏组件集。
/// 文本格式（INI 风格，与 .pri 一致）：
/// <code>
/// [Desktop]
/// Version=0.1
/// Default=ClockCpu
///
/// [Screen.Primary]
/// Components=ClockCpu,Weather
///
/// [Screen.Secondary]
/// Components=IconGrid
/// </code>
/// 匹配键：<c>Primary</c>=主屏；<c>Secondary</c>=首个非主屏；数字=屏幕枚举序号；其余=设备名子串。
/// </summary>
public static class ScreenProfileCatalog
{
    public const string ProfileFileName = "desktop.profile";

    public const string DefaultProfileText = """
        [Desktop]
        Version=0.1
        Default=ClockCpu
        """;

    private static readonly char[] Sep = { ',', ';' };
    private static readonly HashSet<string> SpecialKeys = new(StringComparer.OrdinalIgnoreCase)
        { "Primary", "Secondary" };

    /// <summary>解析配置文本。</summary>
    public static (DesktopProfile Profile, List<Diagnostic> Diagnostics) Parse(string text)
    {
        var diags = new List<Diagnostic>();
        string version = "0.1";
        var defaults = new List<string>();
        var screens = new List<ScreenAssignment>();

        string? current = null;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                current = line[1..^1].Trim();
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();

            if (string.Equals(current, "Desktop", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(key, "Version", StringComparison.OrdinalIgnoreCase)) version = val;
                else if (string.Equals(key, "Default", StringComparison.OrdinalIgnoreCase))
                    defaults.AddRange(SplitComponents(val));
            }
            else if (current is not null && current.StartsWith("Screen", StringComparison.OrdinalIgnoreCase))
            {
                var screenKey = current["Screen".Length..].Trim().TrimStart('.');
                if (string.Equals(key, "Components", StringComparison.OrdinalIgnoreCase))
                    screens.Add(new ScreenAssignment(screenKey, SplitComponents(val)));
            }
        }

        var profile = new DesktopProfile(version, defaults, screens);
        Validate(profile, diags);
        return (profile, diags);
    }

    private static IReadOnlyList<string> SplitComponents(string text)
        => text.Split(Sep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .Where(s => s.Length > 0)
               .ToList();

    /// <summary>把每屏映射到其应加载的组件名列表（按屏幕顺序返回）。</summary>
    /// <param name="profile">桌面多屏配置。</param>
    /// <param name="screens">实际枚举到的屏幕（顺序即匹配顺序）。</param>
    /// <param name="diags">可选，写入诊断（未知组件 / 未命中分配）。</param>
    /// <param name="knownComponents">可选，提供则对未知组件名发出警告。</param>
    public static IReadOnlyList<(ScreenInfo Screen, IReadOnlyList<string> Components)> Resolve(
        DesktopProfile profile,
        IReadOnlyList<ScreenInfo> screens,
        List<Diagnostic>? diags = null,
        IReadOnlySet<string>? knownComponents = null)
    {
        var result = new List<(ScreenInfo, IReadOnlyList<string>)>();
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < screens.Count; i++)
        {
            var screen = screens[i];
            var assignment = MatchAssignment(profile.Screens, screen, i);
            var components = assignment?.Components ?? profile.DefaultComponents;

            if (assignment is not null) usedKeys.Add(assignment.ScreenKey);

            if (knownComponents is not null)
            {
                foreach (var c in components)
                    if (!knownComponents.Contains(c))
                        diags?.Add(new Diagnostic(DiagnosticSeverity.Warning,
                            $"屏幕 '{assignment?.ScreenKey ?? "Default"}' 引用未知组件: {c}",
                            "<profile>", 0, 0, 0, "SCREEN_UNKNOWN_COMPONENT"));
            }

            result.Add((screen, components));
        }

        foreach (var a in profile.Screens)
            if (!usedKeys.Contains(a.ScreenKey))
                diags?.Add(new Diagnostic(DiagnosticSeverity.Warning,
                    $"屏幕分配 '{a.ScreenKey}' 未匹配到任何屏幕", "<profile>", 0, 0, 0, "SCREEN_UNASSIGNED"));

        return result;
    }

    private static ScreenAssignment? MatchAssignment(
        IReadOnlyList<ScreenAssignment> assignments, ScreenInfo screen, int index)
    {
        var primary = assignments.FirstOrDefault(a =>
            string.Equals(a.ScreenKey, "Primary", StringComparison.OrdinalIgnoreCase) && screen.IsPrimary);
        if (primary is not null) return primary;

        if (!screen.IsPrimary)
        {
            var sec = assignments.FirstOrDefault(a =>
                string.Equals(a.ScreenKey, "Secondary", StringComparison.OrdinalIgnoreCase));
            if (sec is not null) return sec;
        }

        var byIndex = assignments.FirstOrDefault(a =>
        {
            if (SpecialKeys.Contains(a.ScreenKey)) return false;
            return int.TryParse(a.ScreenKey, out var n) && n == index;
        });
        if (byIndex is not null) return byIndex;

        return assignments.FirstOrDefault(a =>
            !SpecialKeys.Contains(a.ScreenKey)
            && screen.DeviceName.IndexOf(a.ScreenKey, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>校验配置：重复屏键=错误；空默认/空分配=警告。</summary>
    public static List<Diagnostic> Validate(DesktopProfile profile)
    {
        var diags = new List<Diagnostic>();
        Validate(profile, diags);
        return diags;
    }

    private static void Validate(DesktopProfile profile, List<Diagnostic> diags)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in profile.Screens)
        {
            if (!seen.Add(a.ScreenKey))
                diags.Add(new Diagnostic(DiagnosticSeverity.Error,
                    $"重复屏幕分配键: {a.ScreenKey}", "<profile>", 0, 0, 0, "SCREEN_DUP"));
            if (a.Components.Count == 0)
                diags.Add(new Diagnostic(DiagnosticSeverity.Warning,
                    $"屏幕分配 '{a.ScreenKey}' 未指定任何组件", "<profile>", 0, 0, 0, "SCREEN_EMPTY"));
        }

        if (profile.DefaultComponents.Count == 0)
            diags.Add(new Diagnostic(DiagnosticSeverity.Warning,
                "未设置默认组件，未匹配屏幕将不加载任何组件", "<profile>", 0, 0, 0, "SCREEN_NO_DEFAULT"));
    }

    /// <summary>序列化回文本（供 Studio 回写）。</summary>
    public static string ToText(DesktopProfile profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Desktop]");
        sb.AppendLine($"Version={profile.Version}");
        sb.AppendLine($"Default={string.Join(",", profile.DefaultComponents)}");
        sb.AppendLine();
        foreach (var a in profile.Screens)
        {
            sb.AppendLine($"[Screen.{a.ScreenKey}]");
            sb.AppendLine($"Components={string.Join(",", a.Components)}");
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>解析用户配置文件；不存在时回退内置默认。</summary>
    public static string LoadProfileText()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Prismica", ProfileFileName);
        if (File.Exists(appData)) return File.ReadAllText(appData);

        var bundled = Path.Combine(AppContext.BaseDirectory, "Components", ProfileFileName);
        if (File.Exists(bundled)) return File.ReadAllText(bundled);

        return DefaultProfileText;
    }

    /// <summary>用户配置文件的保存路径（AppData\Prismica\desktop.profile）。</summary>
    public static string ProfileSavePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Prismica", ProfileFileName);
}
