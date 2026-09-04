using System;
using System.Collections.Generic;
using System.Text.Json;
using Prismica.Core.Primitives;
using Prismica.Core.Components;
using Prismica.Core.Parameters;

namespace Prismica.Core.Parsing;

public sealed class IniSkinTextParser : ISkinTextParser
{
    public ParseResult Parse(string text, string filePath = "<memory>")
    {
        var diagnostics = new List<Diagnostic>();
        var lines = text.Split('\n', StringSplitOptions.None);

        string? currentSection = null;
        var sectionData = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var sectionOrder = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            int lineNum = i + 1;
            string rawLine = lines[i];
            string line = rawLine.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith(';') || line.StartsWith('#'))
                continue;

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                if (!sectionData.ContainsKey(currentSection))
                {
                    sectionData[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    sectionOrder.Add(currentSection);
                }
                continue;
            }

            if (currentSection == null)
            {
                diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "Key-value outside any section", filePath, lineNum, 0, rawLine.Length, "ORPHAN_KEY"));
                continue;
            }

            int eqIndex = line.IndexOf('=');
            if (eqIndex < 0)
            {
                diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "Invalid key-value format (missing '=')", filePath, lineNum, 0, rawLine.Length, "INVALID_KV"));
                continue;
            }

            string key = line[..eqIndex].Trim();
            string value = line[(eqIndex + 1)..].Trim();

            if (!sectionData[currentSection].TryAdd(key, value))
            {
                diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Duplicate key '{key}' in section '[{currentSection}]'", filePath, lineNum, 0, rawLine.Length, "DUPLICATE_KEY"));
            }
        }

        // 没有 [Prismica] 段的文件不是合法组件，返回 null 定义（调用方据此跳过/报错）
        if (!sectionData.ContainsKey("Prismica"))
        {
            diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, "Missing required [Prismica] section", filePath, 0, 0, 0, "NO_PRISMICA_SECTION"));
            return new ParseResult(null, diagnostics);
        }

        // 解析各段
        var prismica = ParsePrismicaSection(sectionData, diagnostics, filePath);
        var variables = ParseVariables(sectionData, diagnostics, filePath);
        var globalVariables = ParseGlobalVariables(sectionData, diagnostics, filePath);
        var interfaceSchema = ParseInterface(sectionData, diagnostics, filePath);
        var measures = ParseMeasures(sectionData, diagnostics, filePath);
        var meters = ParseMeters(sectionData, diagnostics, filePath);
        var embeds = ParseEmbeds(sectionData, diagnostics, filePath);
        var styles = ParseStyles(sectionData, diagnostics, filePath);
        var animations = ParseAnimations(sectionData, diagnostics, filePath);

        var definition = new ComponentDefinition(
            prismica.Name, prismica.Version, prismica, variables, globalVariables, interfaceSchema, measures, meters, embeds, styles, animations
        );

        return new ParseResult(definition, diagnostics);
    }

    public ParseResult ParseIncremental(string text, ParseResult previous)
    {
        return Parse(text); // 简化：完全重解析
    }

    private PrismicaSection ParsePrismicaSection(Dictionary<string, Dictionary<string, string>> data, List<Diagnostic> diags, string file)
    {
        if (!data.TryGetValue("Prismica", out var dict)) dict = new();
        return new PrismicaSection(
            dict.TryGetValue("Version", out var v) ? v : "0.1",
            dict.TryGetValue("Name", out var n) ? n : "Unnamed",
            dict.TryGetValue("Author", out var a) ? a : "",
            dict.TryGetValue("Description", out var d) ? d : "",
            int.TryParse(dict.GetValueOrDefault("MeasureGrid", "40"), out var mg) ? mg : 40,
            int.TryParse(dict.GetValueOrDefault("Update", "1000"), out var u) ? u : 1000,
            double.TryParse(dict.GetValueOrDefault("Width", "200"), out var w) ? w : 200,
            double.TryParse(dict.GetValueOrDefault("Height", "120"), out var h) ? h : 120
        );
    }

    private IReadOnlyDictionary<string, ArgbColor> ParseVariables(Dictionary<string, Dictionary<string, string>> data, List<Diagnostic> diags, string file)
    {
        var result = new Dictionary<string, ArgbColor>(StringComparer.OrdinalIgnoreCase);
        if (data.TryGetValue("Variables", out var dict))
        {
            foreach (var (k, v) in dict)
            {
                try { result[k] = ParseColor(v); }
                catch { diags.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Invalid color in Variables: {k}={v}", file, 0, 0, 0, "INVALID_COLOR")); }
            }
        }
        return result;
    }

    /// <summary>解析 [GlobalVariables] 段，返回跨组件共享的全局变量初值（字符串）。键大小写不敏感。</summary>
    private IReadOnlyDictionary<string, string> ParseGlobalVariables(Dictionary<string, Dictionary<string, string>> data, List<Diagnostic> diags, string file)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (data.TryGetValue("GlobalVariables", out var dict))
        {
            foreach (var (k, v) in dict)
            {
                if (!result.TryAdd(k, v))
                    diags.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Duplicate key '{k}' in section '[GlobalVariables]'", file, 0, 0, 0, "DUPLICATE_KEY"));
            }
        }
        return result;
    }

    private ComponentParameterSchema ParseInterface(Dictionary<string, Dictionary<string, string>> data, List<Diagnostic> diags, string file)
    {
        var parameters = new Dictionary<string, ParameterInfo>(StringComparer.OrdinalIgnoreCase);
        var interfaceSections = data.Keys.Where(k => k.StartsWith("Interface", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var section in interfaceSections)
        {
            var dict = data[section];
            string paramName = section == "Interface" ? "" : section["Interface.".Length..];
            if (section == "Interface")
            {
                // 兼容旧式简写：Key: Type=Value
                foreach (var (k, v) in dict)
                {
                    var parts = v.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var type = ParseParamType(parts.FirstOrDefault(p => p.StartsWith("Type="))?["Type=".Length..] ?? "string");
                    var def = parts.FirstOrDefault(p => p.StartsWith("Default="))?["Default=".Length..] ?? "";
                    var desc = parts.FirstOrDefault(p => p.StartsWith("Desc="))?["Desc=".Length..] ?? "";
                    parameters[k] = new ParameterInfo(k, type, ParseDefault(type, def), desc, null, null, null, null, null, true);
                }
            }
            else
            {
                var type = ParseParamType(dict.GetValueOrDefault("Type", "string"));
                var def = ParseDefault(type, dict.GetValueOrDefault("Default", ""));
                var desc = dict.GetValueOrDefault("Desc", "");
                double? min = dict.TryGetValue("Min", out var mv) && double.TryParse(mv, out var m) ? m : null;
                double? max = dict.TryGetValue("Max", out var xv) && double.TryParse(xv, out var x) ? x : null;
                double? step = dict.TryGetValue("Step", out var sv) && double.TryParse(sv, out var s) ? s : null;
                var opts = dict.TryGetValue("Options", out var ov) ? ov.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() : null;
                var applyTo = dict.GetValueOrDefault("ApplyTo");
                parameters[paramName] = new ParameterInfo(paramName, type, def, desc, min, max, step, opts, applyTo, false);
            }
        }
        return new ComponentParameterSchema("Component", parameters);
    }

    private ParameterType ParseParamType(string s) => s.ToLowerInvariant() switch
    {
        "number" => ParameterType.Number,
        "color" => ParameterType.Color,
        "font" => ParameterType.Font,
        "bool" => ParameterType.Bool,
        "select" => ParameterType.Select,
        "slider" => ParameterType.Slider,
        "url" => ParameterType.Url,
        "text" => ParameterType.Text,
        _ => ParameterType.String
    };

    private object ParseDefault(ParameterType type, string s) => type switch
    {
        ParameterType.Number or ParameterType.Slider => double.TryParse(s, out var n) ? n : 0,
        ParameterType.Bool => bool.TryParse(s, out var b) ? b : false,
        ParameterType.Color => ParseColor(s).Value,
        _ => s
    };

    private ArgbColor ParseColor(string s)
    {
        s = s.Trim();
        if (s.StartsWith('#')) s = s[1..];
        if (s.Length == 6) s = "FF" + s;
        if (s.Length != 8) throw new FormatException();
        return new ArgbColor(uint.Parse(s, System.Globalization.NumberStyles.HexNumber));
    }

    private IReadOnlyList<MeasureDefinition> ParseMeasures(Dictionary<string, Dictionary<string, string>> data, List<Diagnostic> diags, string file)
    {
        var list = new List<MeasureDefinition>();
        foreach (var (section, dict) in data)
        {
            if (!section.StartsWith("Measure", StringComparison.OrdinalIgnoreCase)) continue;
            string name = section; // 保留完整节名作为 measure 名（Rainmeter 约定：MeasureName 引用完整节名）
            string type = dict.GetValueOrDefault("Measure", "Calc");
            list.Add(new MeasureDefinition(name, type, dict));
        }
        return list;
    }

    private IReadOnlyList<MeterDefinition> ParseMeters(Dictionary<string, Dictionary<string, string>> data, List<Diagnostic> diags, string file)
    {
        var list = new List<MeterDefinition>();
        foreach (var (section, dict) in data)
        {
            if (!section.StartsWith("Meter", StringComparison.OrdinalIgnoreCase)) continue;
            // [MeterStyle*] 是命名样式段（见 ParseStyles），不是 meter，必须跳过，否则会被误当 String meter 产生幽灵实例。
            if (section.StartsWith("MeterStyle", StringComparison.OrdinalIgnoreCase)) continue;
            string name = section["Meter".Length..];
            string type = dict.GetValueOrDefault("Meter", "String");
            list.Add(new MeterDefinition(name, type, dict));
        }
        return list;
    }

    private IReadOnlyList<EmbedDefinition> ParseEmbeds(Dictionary<string, Dictionary<string, string>> data, List<Diagnostic> diags, string file)
    {
        var list = new List<EmbedDefinition>();
        foreach (var (section, dict) in data)
        {
            if (!section.StartsWith("Embed", StringComparison.OrdinalIgnoreCase)) continue;
            string name = section["Embed".Length..];
            string type = dict.GetValueOrDefault("Embed", "Unknown");
            list.Add(new EmbedDefinition(name, type, dict));
        }
        return list;
    }

    private IReadOnlyList<StyleDefinition> ParseStyles(Dictionary<string, Dictionary<string, string>> data, List<Diagnostic> diags, string file)
    {
        var list = new List<StyleDefinition>();
        foreach (var (section, dict) in data)
        {
            if (section.StartsWith("Style", StringComparison.OrdinalIgnoreCase) || section.StartsWith("MeterStyle", StringComparison.OrdinalIgnoreCase))
            {
                string name = section.StartsWith("Style") ? section["Style".Length..] : section["MeterStyle".Length..];
                list.Add(new StyleDefinition(name, dict));
            }
        }
        return list;
    }

    private IReadOnlyList<AnimationSpec> ParseAnimations(Dictionary<string, Dictionary<string, string>> data, List<Diagnostic> diags, string file)
    {
        var list = new List<AnimationSpec>();
        foreach (var (section, dict) in data)
        {
            if (!section.StartsWith("Animation", StringComparison.OrdinalIgnoreCase)) continue;
            string name = section.Length == "Animation".Length ? "" : section["Animation".Length..];
            list.Add(new AnimationSpec(
                name,
                AnimationSpec.ParseTrigger(dict.GetValueOrDefault("Trigger", "OnShow")),
                dict.GetValueOrDefault("Target", ""),
                AnimationSpec.ParseProperty(dict.GetValueOrDefault("Property", "Opacity")),
                ParseDouble(dict.GetValueOrDefault("From", "0")),
                ParseDouble(dict.GetValueOrDefault("To", "1")),
                ParseInt(dict.GetValueOrDefault("Duration", "300")),
                dict.GetValueOrDefault("Easing", "Linear"),
                ParseBool(dict.GetValueOrDefault("AutoReverse", "False")),
                ParseInt(dict.GetValueOrDefault("Repeat", "0")),
                ParseInt(dict.GetValueOrDefault("Delay", "0"))
            ));
        }
        return list;
    }

    private static double ParseDouble(string s) => double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    private static int ParseInt(string s) => int.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    private static bool ParseBool(string s) => bool.TryParse(s, out var b) && b;
}