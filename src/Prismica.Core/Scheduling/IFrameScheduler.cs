namespace Prismica.Core.Scheduling;

public interface IFrameScheduler : IDisposable
{
    void Start();
    void Stop();
    IDisposable RegisterFrameCallback(Action<FrameContext> callback, FramePriority priority = FramePriority.Normal);
    AnimationHandle RegisterAnimation(AnimationDefinition def);
    void CancelAnimation(AnimationHandle handle);
    void SetTargetFps(int fps);
    FrameContext CurrentFrame { get; }

    /// <summary>当前已注册且未完成的动画数量（用于帧率自适应治理）。</summary>
    int ActiveAnimationCount { get; }
}

public readonly record struct FrameContext(
    long FrameId,
    TimeSpan Elapsed,
    TimeSpan DeltaTime,
    double InterpolationFactor,
    bool IsLowPriority
);

public enum FramePriority { Low = 0, Normal = 1, High = 2, Immediate = 3 }

public readonly record struct AnimationHandle(Guid Id);

public sealed record AnimationDefinition(
    string Id,
    TimeSpan Duration,
    EasingFunction Easing,
    Action<double> OnProgress,
    Action? OnCompleted,
    bool AutoReverse,
    int RepeatCount
);

public delegate double EasingFunction(double t);