namespace Prismica.Core.Components;

/// <summary>动画触发时机。</summary>
public enum AnimationTrigger
{
    OnShow,    // 组件显示时自动播放
    OnHide,    // 组件隐藏时
    OnUpdate,  // 每次度量刷新时
    OnClick,   // 点击目标时
    Manual     // 仅由 API 手动触发（StartManual）
}

/// <summary>可动画的视觉属性。</summary>
public enum AnimationProperty
{
    Opacity,   // 0..1 透明度
    X,         // 平移 X（像素）
    Y,         // 平移 Y（像素）
    ScaleX,    // 横向缩放
    ScaleY,    // 纵向缩放
    Rotation   // 旋转（度）
}

/// <summary>
/// 一条声明式动画规格，对应 .pri 中的 <c>[Animation*]</c> 段。
/// 由 <see cref="Parsing.AnimationSpecSerializer"/> 负责解析/序列化，
/// 由 <see cref="Animation.ComponentAnimator"/> 在运行时驱动。
/// </summary>
public sealed record AnimationSpec(
    string Name,
    AnimationTrigger Trigger,
    string Target,
    AnimationProperty Property,
    double From,
    double To,
    int DurationMs,
    string EasingName,
    bool AutoReverse,
    int Repeat,    // 0=一次, -1=无限, N=重复 N 次
    int DelayMs
)
{
    public static IReadOnlyList<string> KnownTriggers =>
        new[] { "OnShow", "OnHide", "OnUpdate", "OnClick", "Manual" };

    public static IReadOnlyList<string> KnownProperties =>
        new[] { "Opacity", "X", "Y", "ScaleX", "ScaleY", "Rotation" };

    public static AnimationTrigger ParseTrigger(string s) =>
        Enum.TryParse<AnimationTrigger>(s, true, out var t) ? t : AnimationTrigger.OnShow;

    public static AnimationProperty ParseProperty(string s) =>
        Enum.TryParse<AnimationProperty>(s, true, out var p) ? p : AnimationProperty.Opacity;
}
