using System;
using System.Collections.Generic;

namespace Prismica.Core.Parameters;

public sealed record ComponentParameterSchema(
    string ComponentName,
    IReadOnlyDictionary<string, ParameterInfo> Parameters
);

public sealed record ParameterInfo(
    string Key,
    ParameterType Type,
    object DefaultValue,
    string Description,
    double? Min, double? Max, double? Step,
    IReadOnlyList<string>? Options,
    string? ApplyTo,
    bool IsImplicitVariableBinding
);

public enum ParameterType { String, Number, Color, Font, Bool, Select, Slider, Url, Text }