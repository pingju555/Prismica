using System;
using System.Collections.Generic;
using Prismica.Core.Animation;
using Prismica.Core.Components;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Prismica.Core.Scheduling;
using Xunit;

namespace Prismica.Core.Tests.Animation;

public class ComponentAnimatorTests
{
    private sealed class FakeVisual : IVisual
    {
        public Rect Bounds { get; set; }
        public Transform Transform { get; set; } = Transform.Identity;
        public double Opacity { get; set; } = 1;
        public bool IsVisible { get; set; } = true;
        public IVisual? Parent => null;
        public IReadOnlyList<IVisual> Children => Array.Empty<IVisual>();
        public HitTestResult HitTest(Point point) => new(false, null, HitTestAction.None, Point.Zero);
    }

    private sealed class RecordingScheduler : IFrameScheduler
    {
        public List<AnimationDefinition> Registered { get; } = new();
        public FrameContext CurrentFrame { get; } = default;
        public int ActiveAnimationCount => Registered.Count;
        public void Start() { }
        public void Stop() { }
        public IDisposable RegisterFrameCallback(Action<FrameContext> callback, FramePriority priority = FramePriority.Normal)
            => new DummyDisposable();
        public AnimationHandle RegisterAnimation(AnimationDefinition def)
        {
            Registered.Add(def);
            return new AnimationHandle(Guid.NewGuid());
        }
        public void CancelAnimation(AnimationHandle handle) { }
        public void SetTargetFps(int fps) { }
        public void Dispose() { }
        private sealed class DummyDisposable : IDisposable { public void Dispose() { } }
    }

    [Fact]
    public void BuildDefinition_MapsSpecFields()
    {
        var spec = new AnimationSpec("Fade", AnimationTrigger.OnShow, "Box", AnimationProperty.Opacity,
            0, 1, 300, "Linear", true, 2, 50);
        var animator = new ComponentAnimator(new[] { spec }, new RecordingScheduler(), _ => null);

        var def = animator.BuildDefinition(spec, v => { });

        Assert.Equal("Fade", def.Id);
        Assert.Equal(TimeSpan.FromMilliseconds(300), def.Duration);
        Assert.True(def.AutoReverse);
        Assert.Equal(2, def.RepeatCount);
        Assert.NotNull(def.Easing);
        def.OnProgress(0.0); // 不应抛
    }

    [Fact]
    public void StartOnShow_RegistersOnlyOnShowSpecs()
    {
        var onShow = new AnimationSpec("A", AnimationTrigger.OnShow, "Box", AnimationProperty.Opacity, 0, 1, 300, "Linear", false, 0, 0);
        var onClick = new AnimationSpec("B", AnimationTrigger.OnClick, "Box", AnimationProperty.Opacity, 0, 1, 300, "Linear", false, 0, 0);
        var scheduler = new RecordingScheduler();
        var animator = new ComponentAnimator(new[] { onShow, onClick }, scheduler, _ => null);

        animator.StartOnShow();

        Assert.Single(scheduler.Registered);
        Assert.Equal("A", scheduler.Registered[0].Id);
    }

    [Fact]
    public void StartOnClick_RegistersOnlyMatchingTarget()
    {
        var c1 = new AnimationSpec("A", AnimationTrigger.OnClick, "Box", AnimationProperty.Opacity, 0, 1, 300, "Linear", false, 0, 0);
        var c2 = new AnimationSpec("B", AnimationTrigger.OnClick, "Other", AnimationProperty.Opacity, 0, 1, 300, "Linear", false, 0, 0);
        var scheduler = new RecordingScheduler();
        var animator = new ComponentAnimator(new[] { c1, c2 }, scheduler, _ => null);

        animator.StartOnClick("Box");

        Assert.Single(scheduler.Registered);
        Assert.Equal("A", scheduler.Registered[0].Id);
    }

    [Fact]
    public void ApplyValue_Opacity_ClampsToUnitRange()
    {
        var box = new FakeVisual();
        var scheduler = new RecordingScheduler();
        var spec = new AnimationSpec("Fade", AnimationTrigger.OnShow, "Box", AnimationProperty.Opacity, 0, 1, 300, "Linear", false, 0, 0);
        var animator = new ComponentAnimator(new[] { spec }, scheduler, name => name == "Box" ? box : null);
        animator.StartOnShow();

        var def = scheduler.Registered[0];
        def.OnProgress(0.5);
        Assert.Equal(0.5, box.Opacity, 6);
        def.OnProgress(2.0); // 超出上界钳制
        Assert.Equal(1.0, box.Opacity, 6);
        def.OnProgress(-1.0); // 超出下界钳制
        Assert.Equal(0.0, box.Opacity, 6);
    }

    [Fact]
    public void ApplyValue_Transform_X_SetsTranslation()
    {
        var box = new FakeVisual();
        var scheduler = new RecordingScheduler();
        var spec = new AnimationSpec("Move", AnimationTrigger.OnShow, "Box", AnimationProperty.X, 0, 100, 300, "Linear", false, 0, 0);
        var animator = new ComponentAnimator(new[] { spec }, scheduler, name => name == "Box" ? box : null);
        animator.StartOnShow();

        scheduler.Registered[0].OnProgress(0.5);
        Assert.Equal(50, box.Transform.Matrix.M31, 6); // 平移 X
        Assert.Equal(0, box.Transform.Matrix.M32, 6);   // 平移 Y 不变
    }

    [Fact]
    public void ApplyValue_ResolverNull_DoesNotThrow()
    {
        var scheduler = new RecordingScheduler();
        var spec = new AnimationSpec("Fade", AnimationTrigger.OnShow, "Missing", AnimationProperty.Opacity, 0, 1, 300, "Linear", false, 0, 0);
        var animator = new ComponentAnimator(new[] { spec }, scheduler, _ => null);
        animator.StartOnShow();

        var ex = Record.Exception(() => scheduler.Registered[0].OnProgress(0.5));
        Assert.Null(ex);
    }
}
