using System.IO;
using Prismica.Core.Components;
using Prismica.Core.Parsing;
using Xunit;

namespace Prismica.Core.Tests.Components;

/// <summary>
/// 锁定 IconGrid embed 的 .pri 格式契约：解析器必须把 Embed=IconGrid 识别为 embed 关键字，
/// 且 ComponentLibrary 能把它列为可用组件。防止 parser/格式回归。
/// </summary>
public sealed class IconGridEmbedFormatTests
{
    private const string PriText = @"[Prismica]
Version=0.1
Name=IconGrid
Author=Prismica
Description=G3 示例：桌面图标网格
Width=320
Height=320

[EmbedIcons]
Embed=IconGrid
X=0
Y=0
W=320
H=320
Columns=4
ShowLabels=True
SortBy=Name
";

    [Fact]
    public void Parse_EmbedEqualsKeyword_ResolvesIconGrid()
    {
        var result = new IniSkinTextParser().Parse(PriText, "<memory>");

        Assert.NotNull(result.Definition);
        Assert.Single(result.Definition!.Embeds);
        var embed = result.Definition.Embeds[0];
        Assert.Equal("IconGrid", embed.TypeKeyword);
        Assert.Equal("Icons", embed.Name);
    }

    [Fact]
    public void ComponentLibrary_ListsIconGrid_WhenPriPresent()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"prismica-icongrid-test-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "icon-grid.pri"), PriText);

            var lib = new ComponentLibrary(dir);
            var components = lib.GetAvailableComponents();

            Assert.Contains(components, c => c.Name == "IconGrid");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
