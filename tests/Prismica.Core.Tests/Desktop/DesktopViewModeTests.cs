using Prismica.Core.Desktop;
using Xunit;

namespace Prismica.Core.Tests.Desktop;

public class DesktopViewModeTests
{
    [Fact]
    public void Toggle_Flips_Between_Modes()
    {
        Assert.Equal(DesktopViewMode.Layout, DesktopViewModeRules.Toggle(DesktopViewMode.Desktop));
        Assert.Equal(DesktopViewMode.Desktop, DesktopViewModeRules.Toggle(DesktopViewMode.Layout));
    }

    [Fact]
    public void Toggle_Is_Involution()
    {
        var twice = DesktopViewModeRules.Toggle(DesktopViewModeRules.Toggle(DesktopViewMode.Layout));
        Assert.Equal(DesktopViewMode.Layout, twice);
    }

    [Theory]
    [InlineData(DesktopViewMode.Desktop, true, true)]
    [InlineData(DesktopViewMode.Desktop, false, false)]
    [InlineData(DesktopViewMode.Layout, true, false)]
    [InlineData(DesktopViewMode.Layout, false, false)]
    public void ShouldClickThrough_DesktopUsesConfig_LayoutAlwaysOff(
        DesktopViewMode mode, bool configured, bool expected)
    {
        Assert.Equal(expected, DesktopViewModeRules.ShouldClickThrough(mode, configured));
    }

    [Theory]
    [InlineData("Layout", DesktopViewMode.Layout)]
    [InlineData("layout", DesktopViewMode.Layout)]
    [InlineData("DESKTOP", DesktopViewMode.Desktop)]
    [InlineData(null, DesktopViewMode.Desktop)]
    [InlineData("garbage", DesktopViewMode.Desktop)]
    public void Parse_Accepts_CaseInsensitive_OrFallsBack(string? value, DesktopViewMode expected)
    {
        Assert.Equal(expected, DesktopViewModeRules.Parse(value));
    }

    [Theory]
    [InlineData(DesktopViewMode.Desktop, "Desktop Mode")]
    [InlineData(DesktopViewMode.Layout, "Layout Mode")]
    public void ToLabel_Returns_HumanReadable(DesktopViewMode mode, string expected)
    {
        Assert.Equal(expected, DesktopViewModeRules.ToLabel(mode));
    }
}
