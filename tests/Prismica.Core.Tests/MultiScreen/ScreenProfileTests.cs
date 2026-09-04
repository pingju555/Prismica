using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Prismica.Core.MultiScreen;
using Prismica.Core.Native;
using Prismica.Core.Parsing;
using Prismica.Core.Primitives;
using Xunit;

namespace Prismica.Core.Tests.MultiScreen;

public class ScreenProfileTests
{
    private static ScreenInfo Screen(string name, bool primary) =>
        new(name, new Rect(0, 0, 1920, 1080), new Rect(0, 0, 1920, 1080), 1.0, primary);

    private const string Profile = """
        [Desktop]
        Version=0.1
        Default=ClockCpu

        [Screen.Primary]
        Components=ClockCpu,Weather

        [Screen.Secondary]
        Components=IconGrid
        """;

    [Fact]
    public void Parse_Extracts_Default_And_Screens()
    {
        var (profile, diags) = ScreenProfileCatalog.Parse(Profile);
        diags.Should().BeEmpty();
        profile.DefaultComponents.Should().ContainSingle().Which.Should().Be("ClockCpu");
        profile.Screens.Should().HaveCount(2);
        profile.Screens[0].ScreenKey.Should().Be("Primary");
        profile.Screens[0].Components.Should().BeEquivalentTo(new[] { "ClockCpu", "Weather" });
        profile.Screens[1].ScreenKey.Should().Be("Secondary");
        profile.Screens[1].Components.Should().ContainSingle().Which.Should().Be("IconGrid");
    }

    [Fact]
    public void Resolve_PrimaryAndSecondary_Get_Distinct_Components()
    {
        var (profile, _) = ScreenProfileCatalog.Parse(Profile);
        var screens = new List<ScreenInfo> { Screen("Primary", true), Screen("Secondary", false) };

        var resolved = ScreenProfileCatalog.Resolve(profile, screens);

        resolved.Should().HaveCount(2);
        resolved[0].Components.Should().BeEquivalentTo(new[] { "ClockCpu", "Weather" });
        resolved[1].Components.Should().BeEquivalentTo(new[] { "IconGrid" });
    }

    [Fact]
    public void Resolve_FallsBack_To_Default_When_No_Match()
    {
        const string src = """
            [Desktop]
            Default=ClockCpu

            [Screen.Primary]
            Components=Weather
            """;
        var (profile, _) = ScreenProfileCatalog.Parse(src);
        var screens = new List<ScreenInfo> { Screen("P", true), Screen("S", false) };

        var resolved = ScreenProfileCatalog.Resolve(profile, screens);

        resolved[0].Components.Should().ContainSingle().Which.Should().Be("Weather");
        resolved[1].Components.Should().ContainSingle().Which.Should().Be("ClockCpu");
    }

    [Fact]
    public void Resolve_IndexKey_Matches_Screen_By_Order()
    {
        const string src = """
            [Desktop]
            Default=ClockCpu

            [Screen.1]
            Components=IconGrid
            """;
        var (profile, _) = ScreenProfileCatalog.Parse(src);
        var screens = new List<ScreenInfo> { Screen("P", true), Screen("S1", false) };

        var resolved = ScreenProfileCatalog.Resolve(profile, screens);

        resolved[0].Components.Should().ContainSingle().Which.Should().Be("ClockCpu");
        resolved[1].Components.Should().ContainSingle().Which.Should().Be("IconGrid");
    }

    [Fact]
    public void Resolve_NameSubstring_Matches_DeviceName()
    {
        const string src = """
            [Desktop]
            Default=ClockCpu

            [Screen.HDMI]
            Components=Weather
            """;
        var (profile, _) = ScreenProfileCatalog.Parse(src);
        var screens = new List<ScreenInfo> { Screen(@"\\.\DISPLAY1", true), Screen("HDMI-2", false) };

        var resolved = ScreenProfileCatalog.Resolve(profile, screens);

        resolved[0].Components.Should().ContainSingle().Which.Should().Be("ClockCpu");
        resolved[1].Components.Should().ContainSingle().Which.Should().Be("Weather");
    }

    [Fact]
    public void Resolve_EmptyComponents_Yields_Empty_List()
    {
        const string src = """
            [Desktop]
            Default=ClockCpu

            [Screen.Primary]
            Components=
            """;
        var (profile, _) = ScreenProfileCatalog.Parse(src);
        var screens = new List<ScreenInfo> { Screen("P", true) };

        var resolved = ScreenProfileCatalog.Resolve(profile, screens);

        resolved[0].Components.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_Unmatched_Assignment_Warns()
    {
        var (profile, _) = ScreenProfileCatalog.Parse(Profile);
        var diags = new List<Diagnostic>();
        // 只有主屏，Secondary 分配无法命中
        ScreenProfileCatalog.Resolve(profile, new List<ScreenInfo> { Screen("P", true) }, diags);

        diags.Should().Contain(d => d.Code == "SCREEN_UNASSIGNED");
    }

    [Fact]
    public void Validate_Detects_Duplicate_Key_As_Error()
    {
        const string src = """
            [Desktop]
            Default=ClockCpu

            [Screen.Primary]
            Components=A

            [Screen.Primary]
            Components=B
            """;
        var (profile, _) = ScreenProfileCatalog.Parse(src);
        var diags = ScreenProfileCatalog.Validate(profile);

        diags.Should().Contain(d => d.Code == "SCREEN_DUP" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Validate_Warns_When_No_Default()
    {
        const string src = """
            [Screen.Primary]
            Components=A
            """;
        var (profile, _) = ScreenProfileCatalog.Parse(src);
        var diags = ScreenProfileCatalog.Validate(profile);

        diags.Should().Contain(d => d.Code == "SCREEN_NO_DEFAULT");
    }

    [Fact]
    public void Resolve_Warns_On_Unknown_Component_When_KnownSet_Provided()
    {
        var (profile, _) = ScreenProfileCatalog.Parse(Profile);
        var diags = new List<Diagnostic>();
        var known = new HashSet<string> { "ClockCpu" };
        ScreenProfileCatalog.Resolve(profile, new List<ScreenInfo> { Screen("P", true), Screen("S", false) }, diags, known);

        diags.Should().Contain(d => d.Code == "SCREEN_UNKNOWN_COMPONENT");
    }

    [Fact]
    public void ToText_RoundTrips_Parse()
    {
        var (profile, _) = ScreenProfileCatalog.Parse(Profile);
        var text = ScreenProfileCatalog.ToText(profile);
        var (profile2, diags2) = ScreenProfileCatalog.Parse(text);

        diags2.Should().BeEmpty();
        profile2.DefaultComponents.Should().BeEquivalentTo(profile.DefaultComponents);
        profile2.Screens.Should().HaveCount(profile.Screens.Count);
        profile2.Screens[1].Components.Should().BeEquivalentTo(profile.Screens[1].Components);
    }

    [Fact]
    public void DefaultProfileText_Parses_With_ClockCpu_Default()
    {
        var (profile, diags) = ScreenProfileCatalog.Parse(ScreenProfileCatalog.DefaultProfileText);
        diags.Should().BeEmpty();
        profile.DefaultComponents.Should().ContainSingle().Which.Should().Be("ClockCpu");
    }
}
