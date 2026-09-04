using System;
using System.Collections.Generic;
using Prismica.Core.Primitives;

namespace Prismica.Core.Parameters;

/// <summary>
/// 把组件「封装接口」([Interface.*]) 的实例覆盖值，桥接进组件的变量层（[Variables]）。
/// 这是早期架构 §7「参数接口 / 隐式 #Var# 绑定」与 §9「实例覆盖」的未接线缺口：
/// 此前布局实例里的 Interface 覆盖写入了 layout.ini，却从未注入运行时。
///
/// 规则：
/// - 基础变量 = 组件自身的 [Variables]（def.Variables）。
/// - 仅「隐式变量绑定的颜色类」参数（IsImplicitVariableBinding，或 Color 类型且未显式 ApplyTo）
///   会注入颜色变量字典 —— 与 BuiltinMeters 的 #Var# 颜色替换一致。
/// - 取值优先级：实例 override &gt; 参数 DefaultValue &gt; 基础变量；解析失败则回退基础变量。
/// - 非颜色 / 显式 ApplyTo 的路径（ApplyTo 属性绑定）留给后续的属性面板阶段处理，此处不动。
/// </summary>
public static class InterfaceBinder
{
    public static IReadOnlyDictionary<string, ArgbColor> ResolveVariables(
        ComponentParameterSchema? schema,
        IReadOnlyDictionary<string, object>? overrides,
        IReadOnlyDictionary<string, ArgbColor> baseVariables)
    {
        var merged = new Dictionary<string, ArgbColor>(baseVariables, StringComparer.OrdinalIgnoreCase);
        if (schema?.Parameters is null) return merged;
        overrides ??= new Dictionary<string, object>();

        foreach (var p in schema.Parameters.Values)
        {
            bool isImplicitColor = p.IsImplicitVariableBinding
                || (p.Type == ParameterType.Color && string.IsNullOrEmpty(p.ApplyTo));
            if (!isImplicitColor) continue;

            object? raw = overrides.TryGetValue(p.Key, out var o) ? o : p.DefaultValue;
            if (raw is null) continue;

            string? text = raw switch
            {
                string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
                ArgbColor c => c.ToHex(),
                _ => raw.ToString()
            };
            if (text is null) continue;

            if (TryParseColor(text, out var color))
                merged[p.Key] = color;                 // 实例覆盖优先
            else if (!merged.ContainsKey(p.Key)
                     && baseVariables.TryGetValue(p.Key, out var baseC))
                merged[p.Key] = baseC;                 // 解析失败回退基础变量
        }

        return merged;
    }

    /// <summary>解析颜色文本（hex / R,G,B / R,G,B,A）。失败返回 false。</summary>
    public static bool TryParseColor(string text, out ArgbColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string t = text.Trim();

        if (t[0] == '#')
        {
            try { color = ArgbColor.FromHex(t); return true; }
            catch { return false; }
        }

        var parts = t.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is 3 or 4 && Array.TrueForAll(parts, p => byte.TryParse(p, out _)))
        {
            byte r = byte.Parse(parts[0]);
            byte g = byte.Parse(parts[1]);
            byte b = byte.Parse(parts[2]);
            byte a = parts.Length == 4 ? byte.Parse(parts[3]) : (byte)255;
            color = ArgbColor.FromRgba(r, g, b, a);
            return true;
        }

        return false;
    }
}
