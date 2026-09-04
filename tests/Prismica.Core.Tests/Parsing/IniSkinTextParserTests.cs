using Prismica.Core.Parsing;
using Prismica.Core.Components;
using Xunit;
using FluentAssertions;

namespace Prismica.Core.Tests.Parsing;

public class IniSkinTextParserTests
{
    private readonly IniSkinTextParser _parser = new();

    [Fact]
    public void Parse_Minimal_Component_Works()
    {
        string text = @"
[Prismica]
Version=0.1
Name=TestClock
Update=1000

[Variables]
FontColor=#FFFFFFFF

[MeasureTime]
Measure=Time
Format=%H:%M:%S

[MeterDisplay]
Meter=String
MeasureName=MeasureTime
X=10 Y=10 W=180 H=30
FontColor=#FontColor#
Text=[MeasureTime]
";

        var result = _parser.Parse(text);

        result.Success.Should().BeTrue();
        result.Definition.Should().NotBeNull();
        result.Definition!.Name.Should().Be("TestClock");
        result.Definition.Prismica.Update.Should().Be(1000);
        result.Definition.Variables.Should().ContainKey("FontColor");
        result.Definition.Measures.Should().HaveCount(1);
        result.Definition.Measures[0].Name.Should().Be("MeasureTime");
        result.Definition.Measures[0].TypeKeyword.Should().Be("Time");
        result.Definition.Meters.Should().HaveCount(1);
        result.Definition.Meters[0].Name.Should().Be("Display");
        result.Definition.Meters[0].TypeKeyword.Should().Be("String");
    }

    [Fact]
    public void Parse_Interface_Section_Works()
    {
        string text = @"
[Prismica]
Version=0.1
Name=Test

[Interface.Font]
Type=font
Default=Segoe UI

[Interface.Size]
Type=number
Default=14
Min=8
Max=72
";

        var result = _parser.Parse(text);

        result.Success.Should().BeTrue();
        result.Definition!.Interface.Parameters.Should().ContainKey("Font");
        result.Definition.Interface.Parameters["Font"].Type.Should().Be(Prismica.Core.Parameters.ParameterType.Font);
        result.Definition.Interface.Parameters["Font"].DefaultValue.Should().Be("Segoe UI");
        result.Definition.Interface.Parameters["Size"].Type.Should().Be(Prismica.Core.Parameters.ParameterType.Number);
        ((double)result.Definition.Interface.Parameters["Size"].DefaultValue).Should().Be(14);
    }

    [Fact]
    public void Parse_Embed_Track_Works()
    {
        string text = @"
[Prismica]
Version=0.1
Name=MusicCard

[EmbedMusic]
Embed=MusicPlayer
X=0 Y=0 W=240 H=160
";

        var result = _parser.Parse(text);

        result.Success.Should().BeTrue();
        result.Definition!.Embeds.Should().HaveCount(1);
        result.Definition.Embeds[0].Name.Should().Be("Music");
        result.Definition.Embeds[0].TypeKeyword.Should().Be("MusicPlayer");
    }

    [Fact]
    public void Parse_Style_Section_Works()
    {
        string text = @"
[Prismica]
Version=0.1
Name=Test

[StyleText]
FontFace=Segoe UI
FontSize=14

[MeterLabel]
Meter=String
MeterStyle=StyleText
Text=Hello
";

        var result = _parser.Parse(text);

        result.Success.Should().BeTrue();
        result.Definition!.Styles.Should().HaveCount(1);
        result.Definition.Styles[0].Name.Should().Be("Text");
        result.Definition.Meters[0].Fields["MeterStyle"].Should().Be("StyleText");
    }

    [Fact]
    public void Parse_Handles_Comments_And_Empty_Lines()
    {
        string text = @"
; 这是注释
# 也是注释

[Prismica]
Name=Test

[Variables]
; 变量注释
Color=#FF0000FF
";

        var result = _parser.Parse(text);

        result.Success.Should().BeTrue();
        result.Definition.Should().NotBeNull();
    }

    [Fact]
    public void Parse_Reports_Error_For_Invalid_Line()
    {
        string text = @"
[Prismica]
Name=Test
InvalidLineWithoutEquals
";

        var result = _parser.Parse(text);

        result.Success.Should().BeFalse(); // 有警告但不崩
        result.Diagnostics.Should().Contain(d => d.Code == "INVALID_KV");
    }

    [Fact]
    public void Parse_Variables_As_Colors()
    {
        string text = @"
[Prismica]
Version=0.1
Name=Test

[Variables]
Red=#FFFF0000
Blue=#FF0000FF
";

        var result = _parser.Parse(text);

        result.Success.Should().BeTrue();
        result.Definition!.Variables["Red"].Should().Be(new Prismica.Core.Primitives.ArgbColor(0xFFFF0000));
        result.Definition.Variables["Blue"].Should().Be(new Prismica.Core.Primitives.ArgbColor(0xFF0000FF));
    }
}