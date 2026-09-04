using System;
using System.Collections.Generic;
using Prismica.Core.Primitives;
using Prismica.Core.Formula;

namespace Prismica.Core.Measures;

public abstract class MeasureBase : IMeasure
{
    public string Name { get; }
    public abstract MeasureTypeInfo TypeInfo { get; }
    public MeasureValue CurrentValue { get; protected set; }
    public event Action<MeasureValue>? ValueChanged;
    private bool _disposed;

    protected MeasureBase(string name) => Name = name;

    public abstract ValueTask UpdateAsync(MeasureContext ctx, CancellationToken ct = default);
    public virtual void Configure(IReadOnlyDictionary<string, string> fields) { }

    protected void SetValue(MeasureValue value)
    {
        CurrentValue = value;
        ValueChanged?.Invoke(value);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            DisposeCore();
        }
    }

    protected virtual void DisposeCore() { }
}

public sealed class TimeMeasure : MeasureBase
{
    public override MeasureTypeInfo TypeInfo => new(
        "Time", "时间", "System", MeasureValueType.String,
        new Dictionary<string, MeasureFieldInfo>
        {
            ["Format"] = new("Format", "string", "%H:%M:%S", "strftime 格式", false),
            ["TimeZone"] = new("TimeZone", "string", "Local", "时区", false)
        },
        1000, false, "系统时间/日期"
    );

    private string _format = "%H:%M:%S";

    public TimeMeasure(string name) : base(name) { }

    public override void Configure(IReadOnlyDictionary<string, string> fields)
    {
        if (fields.TryGetValue("Format", out var f)) _format = f;
    }

    public override ValueTask UpdateAsync(MeasureContext ctx, CancellationToken ct = default)
    {
        var now = DateTime.Now;
        string formatted = _format switch
        {
            "%H:%M:%S" => now.ToString("HH:mm:ss"),
            "%H:%M" => now.ToString("HH:mm"),
            "%Y-%m-%d" => now.ToString("yyyy-MM-dd"),
            "%Y/%m/%d" => now.ToString("yyyy/MM/dd"),
            _ => now.ToString(_format.Replace("%H", "HH").Replace("%M", "mm").Replace("%S", "ss")
                .Replace("%Y", "yyyy").Replace("%m", "MM").Replace("%d", "dd"))
        };
        SetValue(MeasureValue.FromString(formatted));
        return ValueTask.CompletedTask;
    }
}

public sealed class CpuMeasure : MeasureBase
{
    public override MeasureTypeInfo TypeInfo => new(
        "CPU", "CPU 占用", "System", MeasureValueType.Number,
        new Dictionary<string, MeasureFieldInfo>
        {
            ["Processor"] = new("Processor", "int", "0", "0=总计, 1..N=逻辑核心", false),
            ["Logical"] = new("Logical", "bool", "true", "是否逻辑核心", false)
        },
        1000, false, "CPU 占用率"
    );

    public CpuMeasure(string name) : base(name) { }

    public override ValueTask UpdateAsync(MeasureContext ctx, CancellationToken ct = default)
    {
        // 简化实现：实际需用 PDH 或 PerformanceCounter
        SetValue(MeasureValue.FromNumber(Random.Shared.NextDouble() * 100));
        return ValueTask.CompletedTask;
    }
}

public sealed class MemoryMeasure : MeasureBase
{
    public override MeasureTypeInfo TypeInfo => new(
        "Memory", "内存", "System", MeasureValueType.Number,
        new Dictionary<string, MeasureFieldInfo>
        {
            ["Type"] = new("Type", "string", "Physical", "Physical/Virtual/PageFile", false)
        },
        2000, false, "内存占用"
    );

    public MemoryMeasure(string name) : base(name) { }

    public override ValueTask UpdateAsync(MeasureContext ctx, CancellationToken ct = default)
    {
        // 简化实现
        var total = GC.GetTotalMemory(false);
        SetValue(MeasureValue.FromNumber(total / (1024.0 * 1024.0))); // MB
        return ValueTask.CompletedTask;
    }
}

public sealed class CalcMeasure : MeasureBase
{
    public override MeasureTypeInfo TypeInfo => new(
        "Calc", "计算", "Calc", MeasureValueType.Number,
        new Dictionary<string, MeasureFieldInfo>
        {
            ["Formula"] = new("Formula", "string", "", "引用其他 Measure 的公式", true),
            ["UpdateDivider"] = new("UpdateDivider", "int", "1", "更新分频", false)
        },
        1000, false, "对其他 Measure 进行运算"
    );

    private string _formula = "";
    private IFormulaEngine? _engine;

    public CalcMeasure(string name) : base(name) { }

    public override void Configure(IReadOnlyDictionary<string, string> fields)
    {
        if (fields.TryGetValue("Formula", out var f)) _formula = f;
    }

    public override ValueTask UpdateAsync(MeasureContext ctx, CancellationToken ct = default)
    {
        _engine ??= ctx.FormulaEngine;
        if (string.IsNullOrEmpty(_formula)) { SetValue(MeasureValue.FromNumber(0)); return ValueTask.CompletedTask; }

        var ast = _engine.Parse(_formula);
        var evalCtx = new Formula.EvalContext(
            new Dictionary<string, Formula.FormulaValue>(),
            ctx.AllMeasures,
            new Dictionary<string, object>(),
            ct
        );
        var result = _engine.Evaluate(ast, evalCtx);
        SetValue(MeasureValue.FromNumber(result.AsNumber()));
        return ValueTask.CompletedTask;
    }
}