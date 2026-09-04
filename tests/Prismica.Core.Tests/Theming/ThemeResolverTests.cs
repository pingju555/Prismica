using FluentAssertions;
using Prismica.Core.Components;
using Prismica.Core.Parsing;
using Prismica.Core.Theming;
using Xunit;

namespace Prismica.Core.Tests.Theming;

public class ThemeResolverTests
{
    private const string Sample = """
        [Prismica]
        Theme=Dark

        [Theme.Dark]
        Text=#FFFFFFFF
        Bg=#FF000000
        Accent=#FF0078D4

        [Theme.Light]
        Text=#FF000000
        Bg=#FFFFFFFF

        [Variables]
        MainColor=@Theme.Accent

        [MeterTitle]
        Meter=String
        FontColor=@Theme.Text
        BackColor=@Theme.Bg
        """;

    [Fact]
    public void Resolve_Substitutes_Active_Theme_Tokens()
    {
        var resolved = ThemeResolver.Resolve(Sample);

        resolved.Should().Contain("FontColor=#FFFFFFFF");
        resolved.Should().Contain("BackColor=#FF000000");
        resolved.Should().Contain("MainColor=#FF0078D4");
    }

    [Fact]
    public void Resolve_Preserves_Theme_Sections_Idempotent()
    {
        var resolved = ThemeResolver.Resolve(Sample);
        // 主题段本身不被替换，可再次解析
        var reResolved = ThemeResolver.Resolve(resolved);

        reResolved.Should().Contain("[Theme.Dark]");
        reResolved.Should().Contain("Text=#FFFFFFFF");
        reResolved.Should().Contain("[Theme.Light]");
        reResolved.Should().Be(resolved);
    }

    [Fact]
    public void Resolve_OverrideName_Switches_Active_Theme()
    {
        var resolved = ThemeResolver.Resolve(Sample, overrideName: "Light");

        resolved.Should().Contain("FontColor=#FF000000");
        resolved.Should().Contain("BackColor=#FFFFFFFF");
    }

    [Fact]
    public void Resolve_Unknown_Token_Left_Intact_With_Warning()
    {
        const string src = """
            [Prismica]
            Theme=Dark

            [Theme.Dark]
            Text=#FFFFFFFF

            [MeterX]
            FontColor=@Theme.Missing
            """;
        var diags = new List<Diagnostic>();
        var resolved = ThemeResolver.Resolve(src, diags: diags);

        resolved.Should().Contain("FontColor=@Theme.Missing");
        diags.Should().ContainSingle(d => d.Code == "THEME_UNKNOWN_TOKEN");
    }

    [Fact]
    public void Resolve_NoActiveTheme_Returns_Unchanged()
    {
        const string src = """
            [Prismica]

            [MeterX]
            FontColor=@Theme.Text
            """;
        ThemeResolver.Resolve(src).Should().Be(src);
    }

    [Fact]
    public void Apply_RoundTrips_Themes_And_Active()
    {
        var themes = ThemeCatalog.ExtractThemes(Sample);
        var active = ThemeCatalog.ExtractActiveName(Sample);

        var applied = ThemeCatalog.Apply("; header\n[Prismica]\nName=X\n", themes, active);
        var themes2 = ThemeCatalog.ExtractThemes(applied);
        var active2 = ThemeCatalog.ExtractActiveName(applied);

        themes2.Should().HaveCount(themes.Count);
        themes2[0].Name.Should().Be("Dark");
        themes2[0].Tokens["Accent"].Should().Be("#FF0078D4");
        active2.Should().Be("Dark");
    }

    [Fact]
    public void Apply_Replaces_Old_Theme_Sections()
    {
        var themes = ThemeCatalog.ExtractThemes(Sample);
        // 先应用一次（带旧主题段），再应用一次，应只剩一份
        var once = ThemeCatalog.Apply(Sample, themes, "Dark");
        var twice = ThemeCatalog.Apply(once, themes, "Dark");

        ThemeCatalog.ExtractThemes(twice).Should().HaveCount(2);
    }

    [Fact]
    public void Validate_Detects_Duplicate_And_Missing_Active()
    {
        const string src = """
            [Prismica]
            Theme=Ghost

            [Theme.Dark]
            Text=#FFFFFFFF

            [Theme.Dark]
            Text=#FF000000

            [MeterX]
            FontColor=@Theme.None
            """;
        var diags = ThemeCatalog.Validate(src);

        diags.Should().Contain(d => d.Code == "THEME_DUP");
        diags.Should().Contain(d => d.Code == "THEME_ACTIVE_MISSING");
        diags.Should().Contain(d => d.Code == "THEME_UNKNOWN_TOKEN");
    }
}
