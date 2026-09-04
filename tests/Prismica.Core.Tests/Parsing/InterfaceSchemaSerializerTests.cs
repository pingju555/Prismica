using System.Collections.Generic;
using System.Linq;
using Prismica.Core.Parsing;
using Xunit;

namespace Prismica.Core.Tests.Parsing;

/// <summary>
/// #20 参数 Schema 设计器的纯逻辑契约：Extract 解析 [Interface.*] 段、Apply 回写。
/// 这是沙箱内可验证的核心，Studio UI 只做绑定。
/// </summary>
public sealed class InterfaceSchemaSerializerTests
{
    private const string Sample = @"[Prismica]
Version=0.1
Name=Demo
Width=200
Height=60

[Interface.Title]
Type=Text
Default=Hello
Label=标题

[Interface.Size]
Type=Number
Default=28
Min=8
Max=72
Label=字号

[Interface.Theme]
Type=Select
Default=Dark
Options=Dark,Light,Auto
Label=主题

[Interface]
Type=Legacy
Default=ignored
";

    [Fact]
    public void Extract_ParsesAllInterfaceSections_WithFields()
    {
        var edits = InterfaceSchemaSerializer.Extract(Sample);

        Assert.Equal(3, edits.Count); // 不含裸 [Interface]
        var title = edits.Single(e => e.Name == "Title");
        Assert.Equal("Text", title.Type);
        Assert.Equal("Hello", title.Default);
        Assert.Equal("标题", title.Label);

        var size = edits.Single(e => e.Name == "Size");
        Assert.Equal("Number", size.Type);
        Assert.Equal("28", size.Default);
        Assert.Equal("8", size.Min);
        Assert.Equal("72", size.Max);

        var theme = edits.Single(e => e.Name == "Theme");
        Assert.Equal("Select", theme.Type);
        Assert.Equal("Dark,Light,Auto", theme.Options);
    }

    [Fact]
    public void Extract_IgnoresBareInterfaceSection()
    {
        var edits = InterfaceSchemaSerializer.Extract(Sample);
        Assert.DoesNotContain(edits, e => e.Name == "Legacy" || e.Name == "");
    }

    [Fact]
    public void Apply_RemovesOldSections_AndRegenerates()
    {
        var edits = new List<InterfaceParamEdit>
        {
            new("Title", "Text", "Hi", "标题", null, null, null),
            new("Size", "Slider", "32", "字号", "8", "72", null)
        };

        var result = InterfaceSchemaSerializer.Apply(Sample, edits);

        // 旧的 [Interface.*] 段已被移除（Select 段应消失）
        Assert.DoesNotContain("[Interface.Theme]", result);
        Assert.DoesNotContain("Options=Dark,Light,Auto", result);
        // 新段存在
        Assert.Contains("[Interface.Title]", result);
        Assert.Contains("[Interface.Size]", result);
        Assert.Contains("Type=Slider", result);
        // 非 Interface 内容被保留
        Assert.Contains("[Prismica]", result);
        Assert.Contains("Width=200", result);
    }

    [Fact]
    public void Apply_ThenExtract_RoundTrips()
    {
        var edits = new List<InterfaceParamEdit>
        {
            new("Title", "Text", "Hi", "标题", null, null, null),
            new("Size", "Slider", "32", "字号", "8", "72", null),
            new("Theme", "Select", "Auto", "主题", null, null, "Dark,Light,Auto")
        };

        var applied = InterfaceSchemaSerializer.Apply(Sample, edits);
        var reExtracted = InterfaceSchemaSerializer.Extract(applied);

        Assert.Equal(3, reExtracted.Count);
        Assert.Equal(edits[0], reExtracted[0]);
        Assert.Equal(edits[1], reExtracted[1]);
        Assert.Equal(edits[2], reExtracted[2]);
    }

    [Fact]
    public void Apply_SkipsEmptyName()
    {
        var edits = new List<InterfaceParamEdit>
        {
            new("", "Text", "x", "", null, null, null)
        };
        var result = InterfaceSchemaSerializer.Apply(Sample, edits);
        Assert.DoesNotContain("[Interface.]", result);
    }
}
