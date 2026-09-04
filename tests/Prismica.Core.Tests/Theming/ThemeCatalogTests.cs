using System.Collections.Generic;
using Prismica.Core.Parsing;
using Prismica.Core.Theming;
using Xunit;

namespace Prismica.Core.Tests.Theming;

public class ThemeCatalogTests
{
    private const string Sample = """
[Prismica]
Theme=Dark

[Theme.Dark]
Text=#FFFFFFFF
Bg=#FF000000

[Theme.Light]
Text=#FF000000
Bg=#FFFFFFFF
""";

    [Fact]
    public void ExtractThemes_ParsesThemeSegments()
    {
        var themes = ThemeCatalog.ExtractThemes(Sample);
        Assert.Equal(2, themes.Count);
        Assert.Contains(themes, t => t.Name == "Dark" && t.Tokens["Text"] == "#FFFFFFFF");
        Assert.Contains(themes, t => t.Name == "Light" && t.Tokens["Bg"] == "#FFFFFFFF");
    }

    [Fact]
    public void ExtractActiveName_ReadsPrismicaTheme() =>
        Assert.Equal("Dark", ThemeCatalog.ExtractActiveName(Sample));

    [Fact]
    public void ExtractActiveName_NoTheme_ReturnsNull()
    {
        const string noTheme = "[Prismica]\nName=Foo\n\n[MeterX]\nMeter=String\nText=Hi\n";
        Assert.Null(ThemeCatalog.ExtractActiveName(noTheme));
    }

    [Fact]
    public void ExtractActiveName_MultiplePrismicaSections_TakesLast()
    {
        const string multi = "[Prismica]\nTheme=A\n\n[Prismica]\nTheme=B\n";
        Assert.Equal("B", ThemeCatalog.ExtractActiveName(multi));
    }

    [Fact]
    public void Apply_RoundTrips()
    {
        var themes = ThemeCatalog.ExtractThemes(Sample);
        var active = ThemeCatalog.ExtractActiveName(Sample);
        var applied = ThemeCatalog.Apply("; comment only\n", themes, active);

        var reThemes = ThemeCatalog.ExtractThemes(applied);
        Assert.Equal(2, reThemes.Count);
        Assert.Equal("Dark", ThemeCatalog.ExtractActiveName(applied));
        Assert.Contains(reThemes, t => t.Name == "Light" && t.Tokens["Bg"] == "#FFFFFFFF");
    }

    [Fact]
    public void Apply_PreservesOtherSections()
    {
        const string withMeter = """
[Prismica]
Theme=Dark

[MeterX]
Meter=String
Text=Hi

[Theme.Dark]
C=1

[Theme.Light]
C=2
""";
        var themes = ThemeCatalog.ExtractThemes(withMeter);
        var applied = ThemeCatalog.Apply(withMeter, themes, "Dark");
        Assert.Contains("MeterX", applied);
        Assert.Contains("Text=Hi", applied);
        // 旧主题段被移除后重新追加，故活动主题仍为 Dark
        Assert.Equal("Dark", ThemeCatalog.ExtractActiveName(applied));
        Assert.Equal(2, ThemeCatalog.ExtractThemes(applied).Count);
    }

    [Fact]
    public void Validate_DuplicateThemeName_Error()
    {
        const string dup = "[Prismica]\nTheme=A\n\n[Theme.A]\nC=1\n\n[Theme.A]\nC=2\n";
        var diags = ThemeCatalog.Validate(dup);
        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Error && d.Code == "THEME_DUP");
    }

    [Fact]
    public void Validate_ActiveUndefined_Warning()
    {
        const string undef = "[Prismica]\nTheme=Ghost\n\n[Theme.A]\nC=1\n";
        var diags = ThemeCatalog.Validate(undef);
        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Warning && d.Code == "THEME_ACTIVE_MISSING");
    }

    [Fact]
    public void Validate_UnknownToken_Warning()
    {
        const string unknown = "[Prismica]\nTheme=A\n\n[Theme.A]\nC=1\n\n[MeterX]\nMeter=String\nText=@Theme.Missing\n";
        var diags = ThemeCatalog.Validate(unknown);
        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Warning && d.Code == "THEME_UNKNOWN_TOKEN");
    }
}
