using System;
using System.Collections.Generic;
using Prismica.Core.Primitives;
using Prismica.Core.Parameters;

namespace Prismica.Core.Components;

/// <summary>
/// 组件定义模型（解析器产出，Core DTO）。
/// 对应 component-format-draft-v0.1 §1 总体结构。
/// </summary>
public sealed record ComponentDefinition(
    string Name,
    string Version,
    PrismicaSection Prismica,
    IReadOnlyDictionary<string, ArgbColor> Variables,
    IReadOnlyDictionary<string, string> GlobalVariables,
    ComponentParameterSchema Interface,
    IReadOnlyList<MeasureDefinition> Measures,
    IReadOnlyList<MeterDefinition> Meters,
    IReadOnlyList<EmbedDefinition> Embeds,
    IReadOnlyList<StyleDefinition> Styles,
    IReadOnlyList<AnimationSpec> Animations
);

public sealed record PrismicaSection(
    string Version,
    string Name,
    string Author,
    string Description,
    int MeasureGrid,
    int Update,
    double Width,
    double Height
);

public sealed record MeasureDefinition(string Name, string TypeKeyword, IReadOnlyDictionary<string, string> Fields);
public sealed record MeterDefinition(string Name, string TypeKeyword, IReadOnlyDictionary<string, string> Fields);
public sealed record EmbedDefinition(string Name, string TypeKeyword, IReadOnlyDictionary<string, string> Fields);
public sealed record StyleDefinition(string Name, IReadOnlyDictionary<string, string> Fields);