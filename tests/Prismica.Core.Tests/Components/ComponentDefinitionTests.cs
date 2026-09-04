using Prismica.Core.Components;
using Prismica.Core.Parsing;
using Xunit;
using FluentAssertions;

namespace Prismica.Core.Tests.Components;

public class ComponentDefinitionTests
{
    private readonly IniSkinTextParser _parser = new();

    [Fact]
    public void Parse_DataTrack_Component_Has_Measures_And_Meters()
    {
        string text = @"
[Prismica]
Version=0.1
Name=Clock

[MeasureTime]
Measure=Time

[MeterDisplay]
Meter=String
MeasureName=MeasureTime
";

        var result = _parser.Parse(text);

        result.Success.Should().BeTrue();
        result.Definition!.Measures.Should().HaveCount(1);
        result.Definition.Measures[0].Name.Should().Be("MeasureTime");
        result.Definition.Meters.Should().HaveCount(1);
        result.Definition.Meters[0].Name.Should().Be("Display");
        result.Definition.Embeds.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmbedTrack_Component_Has_Embeds()
    {
        string text = @"
[Prismica]
Version=0.1
Name=MusicCard

[EmbedPlayer]
Embed=MusicPlayer
X=0 Y=0 W=240 H=160
";

        var result = _parser.Parse(text);

        result.Success.Should().BeTrue();
        result.Definition!.Embeds.Should().HaveCount(1);
        result.Definition.Embeds[0].Name.Should().Be("Player");
        result.Definition.Embeds[0].TypeKeyword.Should().Be("MusicPlayer");
    }

    [Fact]
    public void Parse_Mixed_Track_Component_Has_Both()
    {
        string text = @"
[Prismica]
Version=0.1
Name=Mixed

[MeasureCPU]
Measure=CPU

[MeterCPUGraph]
Meter=Progress
MeasureName=MeasureCPU

[EmbedIcon]
Embed=FileIcon
X=10 Y=10
";

        var result = _parser.Parse(text);

        result.Success.Should().BeTrue();
        result.Definition!.Measures.Should().HaveCount(1);
        result.Definition.Meters.Should().HaveCount(1);
        result.Definition.Embeds.Should().HaveCount(1);
    }

    [Fact]
    public void Parameter_Schema_Interface_Parsed_Correctly()
    {
        string text = @"
[Prismica]
Version=0.1
Name=Test

[Interface.Font]
Type=font
Default=Segoe UI
Desc=字体

[Interface.Opacity]
Type=slider
Default=100
Min=0
Max=100
";

        var result = _parser.Parse(text);

        result.Success.Should().BeTrue();
        result.Definition!.Interface.Parameters.Should().HaveCount(2);
        result.Definition.Interface.Parameters["Font"].Type.Should().Be(Prismica.Core.Parameters.ParameterType.Font);
        result.Definition.Interface.Parameters["Opacity"].Type.Should().Be(Prismica.Core.Parameters.ParameterType.Slider);
        ((double)result.Definition.Interface.Parameters["Opacity"].DefaultValue).Should().Be(100);
    }
}