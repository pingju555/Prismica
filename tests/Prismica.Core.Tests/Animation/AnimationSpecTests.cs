using System;
using System.Collections.Generic;
using FluentAssertions;
using Prismica.Core.Animation;
using Prismica.Core.Components;
using Prismica.Core.Parsing;
using Prismica.Core.Primitives;
using Prismica.Core.Rendering;
using Prismica.Core.Scheduling;
using Xunit;

namespace Prismica.Core.Tests.Animation;

public class AnimationSpecTests
{
    private const string Sample = @"
[Prismica]
Version=0.1
Name=Demo

[MeterBox]
Meter=String
Text=Hi

[AnimationFadeIn]
Trigger=OnShow
Target=Box
Property=Opacity
From=0
To=1
Duration=400
Easing=EaseOutQuad
AutoReverse=False
Repeat=0
Delay=0
";

    [Fact]
    public void Extract_Parses_Animation_Section()
    {
        var specs = AnimationSpecSerializer.Extract(Sample);

        specs.Should().HaveCount(1);
        var s = specs[0];
        s.Name.Should().Be("FadeIn");
        s.Trigger.Should().Be(AnimationTrigger.OnShow);
        s.Target.Should().Be("Box");
        s.Property.Should().Be(AnimationProperty.Opacity);
        s.From.Should().Be(0);
        s.To.Should().Be(1);
        s.DurationMs.Should().Be(400);
        s.EasingName.Should().Be("EaseOutQuad");
        s.AutoReverse.Should().BeFalse();
        s.Repeat.Should().Be(0);
        s.DelayMs.Should().Be(0);
    }

    [Fact]
    public void Parse_Through_Parser_Yields_Animations_On_Definition()
    {
        var def = new IniSkinTextParser().Parse(Sample).Definition;

        def!.Animations.Should().HaveCount(1);
        def.Animations[0].Name.Should().Be("FadeIn");
        def.Animations[0].Property.Should().Be(AnimationProperty.Opacity);
    }

    [Fact]
    public void Apply_RoundTrips_Through_Extract()
    {
        var specs = AnimationSpecSerializer.Extract(Sample);
        var written = AnimationSpecSerializer.Apply("; header\n[Prismica]\nName=X\n", specs);
        var reread = AnimationSpecSerializer.Extract(written);

        reread.Should().HaveCount(1);
        reread[0].Name.Should().Be("FadeIn");
        reread[0].Target.Should().Be("Box");
        reread[0].DurationMs.Should().Be(400);
        reread[0].EasingName.Should().Be("EaseOutQuad");
        // 原 .pri 的其它段被保留
        written.Should().Contain("[Prismica]");
    }

    [Fact]
    public void NamedEasing_Resolves_Known_And_Falls_Back()
    {
        NamedEasing.TryResolve("EaseOutQuad", out var fn).Should().BeTrue();
        fn!(0.5).Should().Be(Easing.EaseOutQuad(0.5));

        NamedEasing.TryResolve("DoesNotExist", out _).Should().BeFalse();
        NamedEasing.ResolveOrDefault("DoesNotExist")(0.3).Should().Be(Easing.Linear(0.3));

        NamedEasing.Names.Should().Contain("Linear").And.Contain("EaseInOutBack");
    }

    [Fact]
    public void Validate_Flags_Unknown_Easing_And_Missing_Target()
    {
        var specs = new List<AnimationSpec>
        {
            new("Bad", AnimationTrigger.OnShow, "", AnimationProperty.Opacity, 0, 1, 300, "NoSuchEase", false, 0, 0)
        };
        var diags = AnimationSpecSerializer.Validate(specs);

        diags.Should().Contain(d => d.Code == "ANIM_NO_TARGET");
        diags.Should().Contain(d => d.Code == "ANIM_BAD_EASING");
    }

    [Fact]
    public void ComponentAnimator_BuildDefinition_Maps_Fields()
    {
        var scheduler = new RecordingFrameScheduler();
        var animator = new ComponentAnimator(Array.Empty<AnimationSpec>(), scheduler, _ => null);
        var spec = new AnimationSpec("Fade", AnimationTrigger.OnShow, "Box", AnimationProperty.Opacity,
            0, 1, 250, "EaseOutCubic", true, 3, 0);

        var def = animator.BuildDefinition(spec, _ => { });

        def.Duration.TotalMilliseconds.Should().Be(250);
        def.AutoReverse.Should().BeTrue();
        def.RepeatCount.Should().Be(3);
        def.Easing(0.5).Should().Be(Easing.EaseOutCubic(0.5));
        def.Id.Should().Be("Fade");
    }

    [Fact]
    public void ComponentAnimator_Applies_Opacity_To_Visual()
    {
        var scheduler = new RecordingFrameScheduler();
        var visual = new FakeVisual();
        var spec = new AnimationSpec("Fade", AnimationTrigger.OnShow, "Box", AnimationProperty.Opacity,
            0, 1, 400, "Linear", false, 0, 0);
        var animator = new ComponentAnimator(new List<AnimationSpec> { spec }, scheduler, name => name == "Box" ? visual : null);

        animator.StartOnShow();
        scheduler.Registered.Should().HaveCount(1);
        scheduler.Registered[0].OnProgress!(1.0); // eased=1 -> To

        visual.Opacity.Should().Be(1.0);
    }

    [Fact]
    public void ComponentAnimator_Applies_Transform_To_Visual()
    {
        var scheduler = new RecordingFrameScheduler();
        var visual = new FakeVisual();
        var spec = new AnimationSpec("Move", AnimationTrigger.OnShow, "Box", AnimationProperty.X,
            0, 100, 400, "Linear", false, 0, 0);
        var animator = new ComponentAnimator(new List<AnimationSpec> { spec }, scheduler, name => name == "Box" ? visual : null);

        animator.StartOnShow();
        scheduler.Registered[0].OnProgress!(1.0);

        // 平移 100 → 原点 (0,0) 被映射到 (100,0)
        var mapped = visual.Transform.TransformPoint(new Point(0, 0));
        mapped.X.Should().BeApproximately(100, 1e-6);
        mapped.Y.Should().BeApproximately(0, 1e-6);
    }

    [Fact]
    public void VisualTransformState_Translation_Applied()
    {
        var st = new VisualTransformState { X = 50, Y = 20 };
        var t = st.ToTransform();
        var p = t.TransformPoint(new Point(0, 0));
        p.X.Should().BeApproximately(50, 1e-6);
        p.Y.Should().BeApproximately(20, 1e-6);
    }

    [Fact]
    public void VisualTransformState_Scale_Applied()
    {
        var st = new VisualTransformState { ScaleX = 3, ScaleY = 2 };
        var t = st.ToTransform();
        t.TransformPoint(new Point(1, 0)).X.Should().BeApproximately(3, 1e-6);
        t.TransformPoint(new Point(0, 1)).Y.Should().BeApproximately(2, 1e-6);
    }

    [Fact]
    public void VisualTransformState_Rotation_PreservesLength()
    {
        var st = new VisualTransformState { RotationDeg = 90 };
        var t = st.ToTransform();
        var origin = t.TransformPoint(new Point(0, 0));
        var mapped = t.TransformPoint(new Point(1, 0));
        var dx = mapped.X - origin.X;
        var dy = mapped.Y - origin.Y;
        Math.Sqrt(dx * dx + dy * dy).Should().BeApproximately(1, 1e-6);
    }
}

// ---- 测试替身 ----

internal sealed class RecordingFrameScheduler : IFrameScheduler
{
    public List<AnimationDefinition> Registered { get; } = new();
    public FrameContext CurrentFrame => new(0, TimeSpan.Zero, TimeSpan.Zero, 0, false);
    public void Start() { }
    public void Stop() { }
    public IDisposable RegisterFrameCallback(Action<FrameContext> callback, FramePriority priority = FramePriority.Normal)
        => new Dummy();
    public AnimationHandle RegisterAnimation(AnimationDefinition def)
    {
        Registered.Add(def);
        return new AnimationHandle(Guid.NewGuid());
    }
    public void CancelAnimation(AnimationHandle handle) { }
    public void SetTargetFps(int fps) { }
    public int ActiveAnimationCount => Registered.Count;
    public void Dispose() { }
    private sealed class Dummy : IDisposable { public void Dispose() { } }
}

internal sealed class FakeVisual : IVisual
{
    public Rect Bounds => new(0, 0, 100, 100);
    public Transform Transform { get; set; } = Transform.Identity;
    public double Opacity { get; set; } = 1;
    public bool IsVisible { get; set; } = true;
    public IVisual? Parent => null;
    public IReadOnlyList<IVisual> Children => Array.Empty<IVisual>();
    public HitTestResult HitTest(Point point) => new(false, null, HitTestAction.None, point);
}
