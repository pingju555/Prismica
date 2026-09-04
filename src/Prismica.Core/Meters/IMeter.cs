using System;
using System.Collections.Generic;
using Prismica.Core.Primitives;
using Prismica.Core.Formula;
using Prismica.Core.Rendering;
using Prismica.Core.Measures;

namespace Prismica.Core.Meters;

public interface IMeter : IDisposable
{
    string Name { get; }
    MeterTypeInfo TypeInfo { get; }
    MeterLayout Layout { get; set; }
    MeterStyle? Style { get; set; }
    IReadOnlyList<string> BoundMeasureNames { get; }
    void Configure(IReadOnlyDictionary<string, string> fields);
    ValueTask UpdateAsync(MeterContext ctx, CancellationToken ct = default);
    void Render(IRenderContext renderCtx);
}

public sealed record MeterTypeInfo(
    string Keyword,
    string DisplayName,
    string Category,
    IReadOnlyDictionary<string, MeterFieldInfo> Fields,
    bool SupportsMeasureBinding,
    bool SupportsContainer,
    string Description
);

public sealed record MeterFieldInfo(
    string Name,
    string Type,
    string DefaultValue,
    string Description,
    bool IsBindable
);

public sealed record MeterLayout(
    double X, double Y, double Width, double Height,
    Anchor Anchor = Anchor.TopLeft,
    Transform? Transform = null,
    bool Hidden = false
);

public enum Anchor { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }

public sealed record MeterStyle(
    string Name,
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyList<string> ParentStyles
);

public sealed record MeterContext(
    IReadOnlyDictionary<string, IMeasure> Measures,
    IReadOnlyDictionary<string, ArgbColor> Variables,
    IFormulaEngine FormulaEngine,
    Rect ParentBounds,
    TimeSpan FrameDelta,
    IReadOnlyDictionary<string, string> Globals
);