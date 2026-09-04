using Prismica.Core.Scheduling;
using Xunit;

namespace Prismica.Core.Tests.Scheduling;

public class FrameRateGovernorTests
{
    private static RenderActivity Idle => new();
    private static RenderActivity ActiveAnim => new() { HasActiveAnimations = true };
    private static RenderActivity Dirty => new() { IsDirty = true };
    private static RenderActivity LiveOnly => new() { HasLiveMeters = true };

    [Fact]
    public void Ctor_Clamps_Fps_Ranges()
    {
        var g = new FrameRateGovernor(activeFps: 999, idleFps: 0, liveFps: 500, idleFramesBeforeDrop: 30);
        Assert.Equal(144, g.ActiveFps);
        Assert.Equal(1, g.IdleFps);          // clamped to [1, ActiveFps]
        Assert.Equal(144, g.LiveFps);         // clamped to [IdleFps, ActiveFps]
    }

    [Fact]
    public void Update_ActiveAnimation_Returns_ActiveFps_Changed()
    {
        var g = new FrameRateGovernor();
        var (fps, changed) = g.Update(ActiveAnim);
        Assert.Equal(60, fps);
        Assert.True(changed);
    }

    [Fact]
    public void Update_Dirty_Returns_ActiveFps()
    {
        var g = new FrameRateGovernor();
        var (fps, _) = g.Update(Dirty);
        Assert.Equal(60, fps);
    }

    [Fact]
    public void Update_LiveMetersOnly_Returns_LiveFps()
    {
        var g = new FrameRateGovernor();
        var (fps, changed) = g.Update(LiveOnly);
        Assert.Equal(2, fps);
        Assert.True(changed);
    }

    [Fact]
    public void Update_FullyIdle_BeforeThreshold_StaysActiveFps()
    {
        var g = new FrameRateGovernor(idleFramesBeforeDrop: 30);
        bool changedAfterFirst = false;
        for (int i = 0; i < 29; i++)   // streak 1..29 → 仍在阈值前，保持 ActiveFps
        {
            var (fps, changed) = g.Update(Idle);
            Assert.Equal(60, fps);
            if (i > 0) changedAfterFirst = changedAfterFirst || changed;
        }
        // 仅首帧因从 -1 起跳而 Changed=true；其后同帧率应为 false（不重置定时器）
        Assert.False(changedAfterFirst);
    }

    [Fact]
    public void Update_FullyIdle_AtThreshold_DropsToIdleFps_Changed()
    {
        var g = new FrameRateGovernor(idleFramesBeforeDrop: 30);
        for (int i = 0; i < 29; i++) g.Update(Idle);   // streak 1..29 → ActiveFps
        var (fps, changed) = g.Update(Idle);           // streak 30 → IdleFps
        Assert.Equal(1, fps);
        Assert.True(changed);
    }

    [Fact]
    public void Update_IdleFramesBeforeDrop_Zero_DropsImmediately()
    {
        var g = new FrameRateGovernor(idleFramesBeforeDrop: 0);
        var (fps, changed) = g.Update(Idle);
        Assert.Equal(1, fps);
        Assert.True(changed);
    }

    [Fact]
    public void Update_IdleThenActive_RampsUpImmediately()
    {
        var g = new FrameRateGovernor(idleFramesBeforeDrop: 30);
        for (int i = 0; i < 40; i++) g.Update(Idle);   // 已达 IdleFps
        var (idleFps, _) = g.Update(Idle);
        Assert.Equal(1, idleFps);
        var (activeFps, changed) = g.Update(ActiveAnim); // 迟滞不 delaying 升频
        Assert.Equal(60, activeFps);
        Assert.True(changed);
    }

    [Fact]
    public void Update_SameFps_AcrossFrames_NoChangeFlag()
    {
        var g = new FrameRateGovernor();
        g.Update(ActiveAnim);                          // first: ActiveFps, changed
        var (_, changed) = g.Update(ActiveAnim);       // still ActiveFps
        Assert.False(changed);
    }

    [Fact]
    public void Reset_ReturnsToInitial_ReportsChangeAgain()
    {
        var g = new FrameRateGovernor();
        g.Update(Idle);
        var (_, changedAfterIdle) = g.Update(Idle);
        Assert.False(changedAfterIdle);
        g.Reset();
        var (fps, changedAfterReset) = g.Update(Idle);
        Assert.Equal(60, fps);
        Assert.True(changedAfterReset);
    }
}

public class DefaultFrameSchedulerAnimationCountTests
{
    [Fact]
    public void ActiveAnimationCount_Tracks_RegisterAndCancel()
    {
        using var scheduler = new DefaultFrameScheduler();
        Assert.Equal(0, scheduler.ActiveAnimationCount);

        var def = new AnimationDefinition(
            Id: "a", Duration: TimeSpan.FromMilliseconds(100), Easing: t => t,
            OnProgress: _ => { }, OnCompleted: null, AutoReverse: false, RepeatCount: 1);
        var h1 = scheduler.RegisterAnimation(def);
        var h2 = scheduler.RegisterAnimation(def);
        Assert.Equal(2, scheduler.ActiveAnimationCount);

        scheduler.CancelAnimation(h1);
        Assert.Equal(1, scheduler.ActiveAnimationCount);

        scheduler.CancelAnimation(h2);
        Assert.Equal(0, scheduler.ActiveAnimationCount);
    }
}
