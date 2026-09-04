using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Prismica.Core.Scheduling;

/// <summary>
/// 把缓动函数名（如 "EaseOutQuad"）解析为 <see cref="EasingFunction"/> 委托。
/// 通过反射 <see cref="Easing"/> 的静态方法自动生成名字表，避免与具体实现脱节。
/// </summary>
public static class NamedEasing
{
    private static readonly Dictionary<string, EasingFunction> _byName =
        typeof(Easing)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(double)
                        && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType == typeof(double))
            .ToDictionary(
                m => m.Name,
                m => (EasingFunction)(t => (double)m.Invoke(null, new object[] { t })!),
                StringComparer.OrdinalIgnoreCase);

    /// <summary>所有可用缓动函数名（按字母序）。</summary>
    public static IReadOnlyList<string> Names => _byName.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>尝试按名字解析缓动函数。</summary>
    public static bool TryResolve(string? name, out EasingFunction fn)
        => _byName.TryGetValue(name ?? string.Empty, out fn!);

    /// <summary>解析缓动函数，找不到时回退到 <see cref="Easing.Linear"/>。</summary>
    public static EasingFunction ResolveOrDefault(string? name)
        => TryResolve(name, out var fn) ? fn : Easing.Linear;
}
