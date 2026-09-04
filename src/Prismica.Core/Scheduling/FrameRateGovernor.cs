namespace Prismica.Core.Scheduling;

/// <summary>
/// 一帧的渲染活动快照，由 App 层在每帧开始时提供。纯数据，便于单元测试固定输入。
/// </summary>
public readonly record struct RenderActivity
{
    /// <summary>是否存在正在播放的动画（需要高帧率平滑过渡）。</summary>
    public bool HasActiveAnimations { get; init; }

    /// <summary>自上一帧以来内容是否发生变化（主题切换 / 组件重载 / 数据更新），需要重绘。</summary>
    public bool IsDirty { get; init; }

    /// <summary>是否含有随时间刷新的 meter（时钟 / CPU / 天气），即便空闲也需低频刷新。</summary>
    public bool HasLiveMeters { get; init; }
}

/// <summary>
/// 帧率自适应治理器（纯逻辑，可单测）。
/// 根据 <see cref="RenderActivity"/> 在「活跃帧率 / 低频存活帧率 / 空闲帧率」之间切换，
/// 并带迟滞（hysteresis）避免帧率在边界频繁抖动。
/// 目标：空闲时把帧率压到最低看门狗水平（默认 1fps），大幅降低 CPU 与每帧分配；
/// 动画或内容变化时立即回到活跃帧率（默认 60fps）。
/// </summary>
public sealed class FrameRateGovernor
{
    /// <summary>活跃帧率：有动画或内容变化时采用（默认 60）。</summary>
    public int ActiveFps { get; }

    /// <summary>空闲帧率：完全无活动时的看门狗帧率（默认 1，仅维持热更新探测）。</summary>
    public int IdleFps { get; }

    /// <summary>存活帧率：空闲但含实时 meter（时钟 / CPU）时的低频刷新（默认 2）。</summary>
    public int LiveFps { get; }

    /// <summary>连续空闲多少帧后才降到 <see cref="IdleFps"/>（迟滞，默认 30）。</summary>
    public int IdleFramesBeforeDrop { get; }

    private int _idleStreak;
    private int _lastFps = -1;

    /// <summary>
    /// 构造治理器。所有帧率均被夹取到合法范围：<c>1 ≤ IdleFps ≤ LiveFps ≤ ActiveFps ≤ 144</c>。
    /// </summary>
    public FrameRateGovernor(int activeFps = 60, int idleFps = 1, int liveFps = 2, int idleFramesBeforeDrop = 30)
    {
        ActiveFps = Clamp(activeFps, 1, 144);
        IdleFps = Clamp(idleFps, 1, ActiveFps);
        LiveFps = Clamp(liveFps, IdleFps, ActiveFps);
        IdleFramesBeforeDrop = Math.Max(0, idleFramesBeforeDrop);
    }

    /// <summary>
    /// 消费一帧的活动状态，返回本帧应设的目标帧率以及是否与上一帧应用的帧率不同。
    /// 调用方仅在返回的 <c>Changed == true</c> 时调用 <c>SetTargetFps</c>，避免每帧重置定时器造成额外开销。
    /// </summary>
    public (int Fps, bool Changed) Update(RenderActivity state)
    {
        int desired;
        if (state.HasActiveAnimations || state.IsDirty)
        {
            _idleStreak = 0;
            desired = ActiveFps;
        }
        else if (state.HasLiveMeters)
        {
            _idleStreak = 0;
            desired = LiveFps;
        }
        else
        {
            _idleStreak++;
            desired = _idleStreak >= IdleFramesBeforeDrop ? IdleFps : ActiveFps;
        }

        if (desired != _lastFps)
        {
            _lastFps = desired;
            return (desired, true);
        }

        return (desired, false);
    }

    /// <summary>重置换位状态（如组件重载后重新进入活跃期）。</summary>
    public void Reset()
    {
        _idleStreak = 0;
        _lastFps = -1;
    }

    private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
}
