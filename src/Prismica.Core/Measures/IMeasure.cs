using System;
using System.Collections.Generic;
using Prismica.Core.Primitives;
using Prismica.Core.Formula;

namespace Prismica.Core.Measures;

public interface IMeasure : IDisposable
{
    string Name { get; }
    MeasureTypeInfo TypeInfo { get; }
    MeasureValue CurrentValue { get; }
    event Action<MeasureValue>? ValueChanged;
    ValueTask UpdateAsync(MeasureContext ctx, CancellationToken ct = default);
    void Configure(IReadOnlyDictionary<string, string> fields);
}

public sealed record MeasureTypeInfo(
    string Keyword,
    string DisplayName,
    string Category,
    MeasureValueType ValueType,
    IReadOnlyDictionary<string, MeasureFieldInfo> Fields,
    int DefaultUpdateMs,
    bool RequiresAdmin,
    string Description
);

public sealed record MeasureFieldInfo(
    string Name,
    string Type,
    string DefaultValue,
    string Description,
    bool Required
);

public enum MeasureValueType { Number, String, Boolean, List }

public readonly record struct MeasureValue(
    double? Number,
    string? String,
    IReadOnlyList<object>? List
)
{
    public bool HasValue => Number.HasValue || String != null || List != null;
    public static MeasureValue FromNumber(double n) => new(n, null, null);
    public static MeasureValue FromString(string s) => new(null, s, null);
    public static MeasureValue FromList(IReadOnlyList<object> l) => new(null, null, l);
}

public sealed record MeasureContext(
    IReadOnlyDictionary<string, IMeasure> AllMeasures,
    IReadOnlyDictionary<string, ArgbColor> Variables,
    IFormulaEngine FormulaEngine,
    TimeSpan FrameDelta,
    CancellationToken CancellationToken
);