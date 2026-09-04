using System;
using System.Collections.Generic;
using Prismica.Core.Components;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Prismica.Core.Scheduling;

namespace Prismica.Core.Animation;

/// <summary>
/// 按动画通道累积的变换状态，最终合成为 <see cref="Transform"/>。
/// 每个被动画的目标（meter/measure/embed）维持一份，使 X/Scale/Rotation 可独立动画并正确叠加。
/// </summary>
public sealed class VisualTransformState
{
    public double X;
    public double Y;
    public double ScaleX = 1;
    public double ScaleY = 1;
    public double RotationDeg;

    public Transform ToTransform() =>
        Transform.CreateTranslation(X, Y) *
        Transform.CreateRotation(RotationDeg * Math.PI / 180.0) *
        Transform.CreateScale(ScaleX, ScaleY);
}

/// <summary>
/// 运行时动画驱动器：把 <see cref="AnimationSpec"/> 列表注册到 <see cref="IFrameScheduler"/>，
/// 并按 <c>Trigger</c> 在合适时机启动；<c>OnProgress</c> 把缓动值应用到目标 <see cref="IVisual"/>。
/// 时间推进依赖注入的调度器（真实环境为 <see cref="DefaultFrameScheduler"/>），
/// 本类自身保持纯逻辑，便于单元测试。
/// </summary>
public sealed class ComponentAnimator : IDisposable
{
    private readonly IReadOnlyList<AnimationSpec> _specs;
    private readonly IFrameScheduler _scheduler;
    private readonly Func<string, IVisual?> _resolver;
    private readonly Dictionary<string, VisualTransformState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AnimationHandle> _handles = new();

    public ComponentAnimator(IReadOnlyList<AnimationSpec> specs, IFrameScheduler scheduler, Func<string, IVisual?> resolver)
    {
        _specs = specs ?? Array.Empty<AnimationSpec>();
        _scheduler = scheduler;
        _resolver = resolver;
    }

    /// <summary>组件显示时启动所有 OnShow 动画。</summary>
    public void StartOnShow()
    {
        foreach (var spec in _specs)
            if (spec.Trigger == AnimationTrigger.OnShow)
                StartSpec(spec);
    }

    /// <summary>某可视对象被点击时启动 OnClick 动画。</summary>
    public void StartOnClick(string target)
    {
        foreach (var spec in _specs)
            if (spec.Trigger == AnimationTrigger.OnClick &&
                string.Equals(spec.Target, target, StringComparison.OrdinalIgnoreCase))
                StartSpec(spec);
    }

    /// <summary>按名称手动启动（Manual 动画或任意动画）。</summary>
    public void StartManual(string name)
    {
        foreach (var spec in _specs)
            if (string.Equals(spec.Name, name, StringComparison.OrdinalIgnoreCase))
                StartSpec(spec);
    }

    private void StartSpec(AnimationSpec spec)
    {
        var handle = _scheduler.RegisterAnimation(BuildDefinition(spec, v => ApplyValue(spec, v)));
        _handles.Add(handle);
    }

    /// <summary>
    /// 把一条规格转换为调度器用的 <see cref="Scheduling.AnimationDefinition"/>。
    /// 缓动函数经 <see cref="NamedEasing"/> 解析；Duration/AutoReverse/Repeat 直接映射。
    /// 返回的 def 不处理 Delay（由上层在启动前延时注册，保持本方法可测）。
    /// </summary>
    public Scheduling.AnimationDefinition BuildDefinition(AnimationSpec spec, Action<double> onProgress)
    {
        var easing = NamedEasing.ResolveOrDefault(spec.EasingName);
        return new Scheduling.AnimationDefinition(
            spec.Name,
            TimeSpan.FromMilliseconds(spec.DurationMs),
            easing,
            onProgress,
            null,
            spec.AutoReverse,
            spec.Repeat);
    }

    private void ApplyValue(AnimationSpec spec, double eased)
    {
        var visual = _resolver(spec.Target);
        if (visual is null) return;
        var value = spec.From + (spec.To - spec.From) * eased;

        if (spec.Property == AnimationProperty.Opacity)
        {
            visual.Opacity = Clamp(value, 0, 1);
            return;
        }

        var st = GetState(spec.Target);
        switch (spec.Property)
        {
            case AnimationProperty.X: st.X = value; break;
            case AnimationProperty.Y: st.Y = value; break;
            case AnimationProperty.ScaleX: st.ScaleX = value; break;
            case AnimationProperty.ScaleY: st.ScaleY = value; break;
            case AnimationProperty.Rotation: st.RotationDeg = value; break;
        }
        visual.Transform = st.ToTransform();
    }

    private VisualTransformState GetState(string target)
    {
        if (!_states.TryGetValue(target, out var st))
        {
            st = new VisualTransformState();
            _states[target] = st;
        }
        return st;
    }

    public void Dispose()
    {
        foreach (var h in _handles) _scheduler.CancelAnimation(h);
        _handles.Clear();
    }

    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
}
