using System.Collections.Generic;
using Prismica.Core.Components;
using Prismica.Core.Parsing;
using Xunit;

namespace Prismica.Core.Tests.Parsing;

public class AnimationSpecSerializerTests
{
    private const string Sample = """
[Prismica]
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
Duration=300
Easing=Linear
""";

    [Fact]
    public void Extract_ParsesAnimationBlock()
    {
        var specs = AnimationSpecSerializer.Extract(Sample);
        Assert.Single(specs);
        var s = specs[0];
        Assert.Equal("FadeIn", s.Name);
        Assert.Equal(AnimationTrigger.OnShow, s.Trigger);
        Assert.Equal("Box", s.Target);
        Assert.Equal(AnimationProperty.Opacity, s.Property);
        Assert.Equal(0, s.From);
        Assert.Equal(1, s.To);
        Assert.Equal(300, s.DurationMs);
        Assert.Equal("Linear", s.EasingName);
    }

    [Fact]
    public void Extract_MissingFields_UsesDefaults()
    {
        const string minimal = "[Animation]\nTarget=X\n";
        var specs = AnimationSpecSerializer.Extract(minimal);
        Assert.Single(specs);
        var s = specs[0];
        Assert.Equal("", s.Name);
        Assert.Equal(AnimationTrigger.OnShow, s.Trigger);
        Assert.Equal(AnimationProperty.Opacity, s.Property);
        Assert.Equal(300, s.DurationMs);
        Assert.Equal("Linear", s.EasingName);
    }

    [Fact]
    public void Apply_RoundTrips()
    {
        var specs = AnimationSpecSerializer.Extract(Sample);
        var applied = AnimationSpecSerializer.Apply("[MeterBox]\nMeter=String\nText=Hi\n", specs);
        var round = AnimationSpecSerializer.Extract(applied);
        Assert.Single(round);
        var s = round[0];
        Assert.Equal("FadeIn", s.Name);
        Assert.Equal("Box", s.Target);
        Assert.Equal(300, s.DurationMs);
        Assert.Equal("Linear", s.EasingName);
    }

    [Fact]
    public void Validate_BadEasing_Error()
    {
        var specs = new List<AnimationSpec>
        {
            new("X", AnimationTrigger.OnShow, "Box", AnimationProperty.Opacity, 0, 1, 300, "Nope", false, 0, 0)
        };
        var diags = AnimationSpecSerializer.Validate(specs);
        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Error && d.Code == "ANIM_BAD_EASING");
    }

    [Fact]
    public void Validate_MissingTarget_Error()
    {
        var specs = new List<AnimationSpec>
        {
            new("X", AnimationTrigger.OnShow, "", AnimationProperty.Opacity, 0, 1, 300, "Linear", false, 0, 0)
        };
        var diags = AnimationSpecSerializer.Validate(specs);
        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Error && d.Code == "ANIM_NO_TARGET");
    }

    [Fact]
    public void Validate_NegativeDuration_Error()
    {
        var specs = new List<AnimationSpec>
        {
            new("X", AnimationTrigger.OnShow, "Box", AnimationProperty.Opacity, 0, 1, 0, "Linear", false, 0, 0)
        };
        var diags = AnimationSpecSerializer.Validate(specs);
        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Error && d.Code == "ANIM_BAD_DURATION");
    }

    [Fact]
    public void Validate_UnknownTarget_WhenKnownProvided_Warning()
    {
        var specs = new List<AnimationSpec>
        {
            new("X", AnimationTrigger.OnShow, "Ghost", AnimationProperty.Opacity, 0, 1, 300, "Linear", false, 0, 0)
        };
        var diags = AnimationSpecSerializer.Validate(specs, new[] { "Box" });
        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Warning && d.Code == "ANIM_TARGET_UNKNOWN");
    }

    [Fact]
    public void Validate_DuplicateName_Warning()
    {
        var specs = new List<AnimationSpec>
        {
            new("Dup", AnimationTrigger.OnShow, "Box", AnimationProperty.Opacity, 0, 1, 300, "Linear", false, 0, 0),
            new("Dup", AnimationTrigger.OnShow, "Box", AnimationProperty.Opacity, 0, 1, 300, "Linear", false, 0, 0)
        };
        var diags = AnimationSpecSerializer.Validate(specs, new[] { "Box" });
        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Warning && d.Code == "ANIM_DUP_NAME");
    }

    [Fact]
    public void Validate_NoName_Warning()
    {
        var specs = new List<AnimationSpec>
        {
            new("", AnimationTrigger.OnShow, "Box", AnimationProperty.Opacity, 0, 1, 300, "Linear", false, 0, 0)
        };
        var diags = AnimationSpecSerializer.Validate(specs, new[] { "Box" });
        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Warning && d.Code == "ANIM_NO_NAME");
    }
}
