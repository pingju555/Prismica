using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Prismica.Core.Components;
using Prismica.Core.Formula;
using Prismica.Core.Measures;
using Prismica.Core.Meters;
using Prismica.Core.Native;
using Prismica.Core.Parameters;
using Prismica.Core.Rendering;
using Prismica.Core.Scheduling;
using Prismica.Core.Styling;
using Point = Prismica.Core.Primitives.Point;
using Rect = Prismica.Core.Primitives.Rect;

namespace Prismica.App;

/// <summary>
/// 组件运行时：从一个 ComponentDefinition 实例化 measures/meters/embeds，
/// 提供更新循环与渲染入口。这是 G3 把 .pri 真正接通 Desktop 的核心。
/// 纯 Core 依赖（无 WPF），可独立单测。
/// </summary>
public sealed class ComponentRuntime : IDisposable
{
    private ComponentRuntime(ComponentDefinition def,
        IReadOnlyDictionary<string, IMeasure> measures,
        IReadOnlyList<IMeter> meters,
        IReadOnlyList<IEmbedHost> embeds,
        IReadOnlyDictionary<string, string> diagnostics,
        IReadOnlyDictionary<string, string> globals,
        IFormulaEngine engine)
    {
        Definition = def;
        Measures = measures;
        Meters = meters;
        Embeds = embeds;
        Diagnostics = diagnostics;
        _globals = globals;
        _engine = engine;
    }

    public ComponentDefinition Definition { get; }
    public IReadOnlyDictionary<string, IMeasure> Measures { get; }
    public IReadOnlyList<IMeter> Meters { get; }
    public IReadOnlyList<IEmbedHost> Embeds { get; }
    /// <summary>构造时无法识别的 keyword → 说明（如 "Meter:foo 未知类型 Bar"）。</summary>
    public IReadOnlyDictionary<string, string> Diagnostics { get; }

    /// <summary>跨组件共享的全局变量（来自共享 GlobalVariableStore），供 #gv:Name# 替换读取。</summary>
    private readonly IReadOnlyDictionary<string, string> _globals;

    /// <summary>公式引擎（来自 Create 的 engine 参数），供 Calc 等度量在 UpdateAsync 中求值。此前写死为 null! 导致 Calc 度量运行期 NPE。</summary>
    private readonly IFormulaEngine _engine;

    /// <summary>从定义构造运行时。未知 measure/meter 类型跳过并记录诊断（不崩）。</summary>
    /// <param name="native">真实桌面抽象（图标枚举/打开等）。可为 null（如 Studio 编辑预览），此时依赖 Native 的 embed 优雅降级。</param>
    /// <param name="globals">跨组件共享的全局变量存储（GlobalVariableStore）。为 null 时使用组件私有空存储。</param>
    public static ComponentRuntime Create(ComponentDefinition def, IFormulaEngine engine, INativeDesktop? native = null, GlobalVariableStore? globals = null)
    {
        var store = globals ?? new GlobalVariableStore();
        // 把 .pri 的 [GlobalVariables] 初值 seed 进共享存储（仅当变量尚不存在，避免覆盖运行期已变更的值）。
        foreach (var (k, v) in def.GlobalVariables)
            store.TryAdd(k, v);

        var diags = new Dictionary<string, string>();

        var measures = new Dictionary<string, IMeasure>(StringComparer.OrdinalIgnoreCase);
        foreach (var md in def.Measures)
        {
            var factory = MeasureRegistry.Resolve(md.TypeKeyword);
            if (factory is null) { diags[$"Measure:{md.Name}"] = $"未知 Measure 类型 {md.TypeKeyword}"; continue; }
            var im = factory(md.Name);
            im.Configure(md.Fields);
            measures[md.Name] = im;
        }

        var meters = new List<IMeter>();
        foreach (var m in def.Meters)
        {
            var factory = MeterRegistry.Resolve(m.TypeKeyword);
            if (factory is null) { diags[$"Meter:{m.Name}"] = $"未知 Meter 类型 {m.TypeKeyword}"; continue; }
            var meter = factory(m.Name);
            // MeterStyle 继承：把 meter 引用的命名样式字段合并进自身字段（自身覆盖样式），支持样式嵌套 + 环检测。
            var resolved = MeterStyleResolver.Resolve(m.Fields, def.Styles);
            if (resolved.MissingStyles.Count > 0)
                diags[$"Meter:{m.Name}"] = "未知样式引用: " + string.Join(", ", resolved.MissingStyles);
            meter.Style = new MeterStyle(m.Name, resolved.MergedFields, resolved.ParentStyles);
            meter.Configure(resolved.MergedFields);
            meters.Add(meter);
        }

        var embeds = new List<IEmbedHost>();
        foreach (var ed in def.Embeds)
        {
            var comp = EmbedRegistry.Resolve(ed.TypeKeyword);
            if (comp is null) { diags[$"Embed:{ed.Name}"] = $"未知 Embed 类型 {ed.TypeKeyword}"; continue; }
            var ectx = new EmbedContext(measures, def.Variables, engine, native, CancellationToken.None);
            embeds.Add(comp.CreateHost(ed, ectx));
        }

        return new ComponentRuntime(def, measures, meters, embeds, diags, store, engine);
    }

    /// <summary>
    /// 全量更新：先所有 measure 再所有 meter（meter 可引用 measure 当前值）。
    /// 由帧调度驱动；返回本帧是否发生变化（供重绘判断）。
    /// </summary>
    public async ValueTask<bool> UpdateAsync(TimeSpan frameDelta, CancellationToken ct = default)
    {
        var mctx = new MeasureContext(Measures, Definition.Variables, _engine, frameDelta, ct);
        foreach (var m in Measures.Values)
            await m.UpdateAsync(mctx, ct);

        var octx = new MeterContext(Measures, Definition.Variables, null!, new Rect(0, 0, Definition.Prismica.Width, Definition.Prismica.Height), frameDelta, _globals);
        foreach (var meter in Meters)
            await meter.UpdateAsync(octx, ct);

        var fctx = new FrameContext(0, TimeSpan.Zero, frameDelta, 1.0, false);
        foreach (var e in Embeds)
            e.OnFrame(fctx);

        return true;
    }

    /// <summary>把 meter 树渲染到指定 IRenderContext（embed 由 WPF 桥接层另行渲染）。</summary>
    public void RenderAll(IRenderContext rc)
    {
        foreach (var meter in Meters)
            meter.Render(rc);
    }

    /// <summary>命中测试：返回命中的 meter/embed 名称与动作；未命中返回 null。</summary>
    public HitTestResult? HitTest(Point p)
    {
        foreach (var meter in Meters)
        {
            if (meter.Layout == null) continue;
            var r = new Rect(meter.Layout.X, meter.Layout.Y, meter.Layout.Width, meter.Layout.Height);
            if (r.Contains(p)) return new HitTestResult(true, meter.Name, HitTestAction.Click, p);
        }
        foreach (var e in Embeds)
        {
            var hr = e.HitTest(p);
            if (hr.Hit) return hr;
        }
        return null;
    }

    public void Dispose()
    {
        foreach (var m in Measures.Values) m.Dispose();
        foreach (var meter in Meters) meter.Dispose();
        foreach (var e in Embeds) e.Dispose();
    }
}

/// <summary>按类型关键字解析 Measure 工厂（Core 内建 + 可扩展）。</summary>
public static class MeasureRegistry
{
    private static readonly Dictionary<string, Func<string, IMeasure>> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Time"] = n => new TimeMeasure(n),
        ["CPU"] = n => new CpuMeasure(n),
        ["Memory"] = n => new MemoryMeasure(n),
        ["Calc"] = n => new CalcMeasure(n),
    };

    public static Func<string, IMeasure>? Resolve(string keyword) => Registry.TryGetValue(keyword, out var f) ? f : null;
}

/// <summary>按类型关键字解析 Meter 工厂（Core 内建 + 可扩展）。</summary>
public static class MeterRegistry
{
    private static readonly Dictionary<string, Func<string, IMeter>> Registry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["String"] = n => new StringMeter(n),
        ["Progress"] = n => new ProgressMeter(n),
        ["Container"] = n => new ContainerMeter(n),
    };

    public static Func<string, IMeter>? Resolve(string keyword) => Registry.TryGetValue(keyword, out var f) ? f : null;
}

/// <summary>按类型关键字解析 Embed 组件（现为注册表，无内建实现）。</summary>
public static class EmbedRegistry
{
    private static readonly Dictionary<string, Func<IEmbedComponent>> Registry = new(StringComparer.OrdinalIgnoreCase);

    public static IEmbedComponent? Resolve(string keyword) => Registry.TryGetValue(keyword, out var f) ? f() : null;

    public static void Register(string keyword, Func<IEmbedComponent> factory)
        => Registry[keyword] = factory;
}
