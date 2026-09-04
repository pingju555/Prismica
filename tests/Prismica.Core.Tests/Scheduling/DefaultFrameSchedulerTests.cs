using Prismica.Core.Scheduling;
using Xunit;
using FluentAssertions;

namespace Prismica.Core.Tests.Scheduling;

public class DefaultFrameSchedulerTests
{
    [Fact]
    public void Start_Stop_Works()
    {
        using var scheduler = new DefaultFrameScheduler();
        scheduler.Start();
        scheduler.Stop();
    }

    [Fact]
    public void RegisterFrameCallback_Invoked()
    {
        using var scheduler = new DefaultFrameScheduler();
        int count = 0;
        var disposable = scheduler.RegisterFrameCallback(_ => count++);
        scheduler.Start();
        Thread.Sleep(50);
        scheduler.Stop();
        disposable.Dispose();
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RegisterFrameCallback_Disposable_Stops_Callback()
    {
        using var scheduler = new DefaultFrameScheduler();
        int count = 0;
        var disposable = scheduler.RegisterFrameCallback(_ => count++);
        scheduler.Start();
        Thread.Sleep(30);
        disposable.Dispose();
        int countAfterDispose = count;
        Thread.Sleep(30);
        scheduler.Stop();
        count.Should().Be(countAfterDispose);
    }

    [Fact]
    public void RegisterAnimation_Executes_Progress()
    {
        using var scheduler = new DefaultFrameScheduler();
        double progress = 0;
        var handle = scheduler.RegisterAnimation(new AnimationDefinition(
            "test", TimeSpan.FromMilliseconds(50), Easing.Linear,
            p => progress = p, null, false, 1
        ));
        scheduler.Start();
        Thread.Sleep(100);
        scheduler.Stop();
        progress.Should().BeApproximately(1.0, 0.1);
    }

    [Fact]
    public void CancelAnimation_Stops_Animation()
    {
        using var scheduler = new DefaultFrameScheduler();
        double progress = 0;
        var handle = scheduler.RegisterAnimation(new AnimationDefinition(
            "test", TimeSpan.FromMilliseconds(200), Easing.Linear,
            p => progress = p, null, false, 1
        ));
        scheduler.Start();
        Thread.Sleep(50);
        scheduler.CancelAnimation(handle);
        double progressAtCancel = progress;
        Thread.Sleep(200);
        scheduler.Stop();
        progress.Should().BeApproximately(progressAtCancel, 0.1);
    }

    [Fact]
    public void SetTargetFps_Changes_Interval()
    {
        using var scheduler = new DefaultFrameScheduler();
        scheduler.SetTargetFps(30);
        scheduler.SetTargetFps(120);
    }
}