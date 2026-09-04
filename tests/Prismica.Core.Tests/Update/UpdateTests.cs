using Prismica.Core.Update;
using Xunit;

namespace Prismica.Core.Tests.Update;

public class SemVersionTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3, null)]
    [InlineData("v2.0.0", 2, 0, 0, null)]
    [InlineData("V3.4.5", 3, 4, 5, null)]
    [InlineData("1.2.3-beta.1", 1, 2, 3, "beta.1")]
    [InlineData("1.2", 1, 2, 0, null)]
    [InlineData("1", 1, 0, 0, null)]
    [InlineData("  4.5.6  ", 4, 5, 6, null)]
    public void TryParse_Valid(string input, int major, int minor, int patch, string? pre)
    {
        Assert.True(SemVersion.TryParse(input, out var v));
        Assert.NotNull(v);
        Assert.Equal(major, v!.Major);
        Assert.Equal(minor, v.Minor);
        Assert.Equal(patch, v.Patch);
        Assert.Equal(pre, v.Prerelease);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.x")]
    [InlineData("-1.0.0")]
    public void TryParse_Invalid(string? input)
    {
        Assert.False(SemVersion.TryParse(input, out var v));
        Assert.Null(v);
    }

    [Fact]
    public void Compare_Equal()
    {
        SemVersion.TryParse("1.0.0", out var a);
        SemVersion.TryParse("1.0.0", out var b);
        Assert.Equal(0, a!.CompareTo(b));
    }

    [Fact]
    public void Compare_NewerPatch()
    {
        SemVersion.TryParse("1.2.3", out var lo);
        SemVersion.TryParse("1.2.4", out var hi);
        Assert.True(lo!.CompareTo(hi) < 0);
        Assert.True(hi!.CompareTo(lo) > 0);
    }

    [Fact]
    public void Compare_MajorBeatsPrerelease()
    {
        SemVersion.TryParse("1.2.3", out var stable);
        SemVersion.TryParse("1.2.3-beta", out var pre);
        Assert.True(stable!.CompareTo(pre) > 0);
        Assert.True(pre!.CompareTo(stable) < 0);
    }

    [Fact]
    public void Compare_PrereleaseOrdering()
    {
        SemVersion.TryParse("1.0.0-beta.1", out var a);
        SemVersion.TryParse("1.0.0-beta.2", out var b);
        SemVersion.TryParse("1.0.0-alpha", out var c);
        Assert.True(a!.CompareTo(b) < 0);
        Assert.True(c!.CompareTo(a) < 0);
        Assert.True(c.CompareTo(b) < 0);
    }

    [Fact]
    public void Compare_MajorMinor()
    {
        SemVersion.TryParse("2.0.0", out var a);
        SemVersion.TryParse("1.9.9", out var b);
        Assert.True(a!.CompareTo(b) > 0);
    }

    [Fact]
    public void IsPrerelease_Flag()
    {
        SemVersion.TryParse("1.0.0-rc", out var pre);
        SemVersion.TryParse("1.0.0", out var stable);
        Assert.True(pre!.IsPrerelease);
        Assert.False(stable!.IsPrerelease);
    }
}

public class UpdateManifestTests
{
    private const string ValidJson = """
    {
      "version": "1.2.0",
      "channel": "stable",
      "notes": "修复若干崩溃",
      "downloadUrl": "https://example.com/Prismica-1.2.0.msi",
      "minRequiredVersion": "1.0.0",
      "publishedAt": "2026-09-01T08:00:00Z"
    }
    """;

    [Fact]
    public void FromJson_ParsesAllFields()
    {
        var m = UpdateManifest.FromJson(ValidJson);
        Assert.NotNull(m);
        Assert.Equal("1.2.0", m!.Version);
        Assert.Equal("stable", m.Channel);
        Assert.Equal("修复若干崩溃", m.Notes);
        Assert.Equal("https://example.com/Prismica-1.2.0.msi", m.DownloadUrl);
        Assert.Equal("1.0.0", m.MinRequiredVersion);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero), m.PublishedAt);
    }

    [Fact]
    public void FromJson_MissingVersion_ReturnsNull()
    {
        Assert.Null(UpdateManifest.FromJson("""{"channel":"stable"}"""));
    }

    [Fact]
    public void FromJson_NullOrWhitespace_ReturnsNull()
    {
        Assert.Null(UpdateManifest.FromJson(null));
        Assert.Null(UpdateManifest.FromJson("   "));
        Assert.Null(UpdateManifest.FromJson("not json"));
    }

    [Fact]
    public void FromJson_PartialFields_Ok()
    {
        var m = UpdateManifest.FromJson("""{"version":"0.9.0"}""");
        Assert.NotNull(m);
        Assert.Equal("0.9.0", m!.Version);
        Assert.Null(m.Channel);
        Assert.Null(m.DownloadUrl);
    }
}

public class UpdateDecisionTests
{
    private static readonly SemVersion Current = new(1, 1, 0);

    [Fact]
    public void Evaluate_NoUpdate_WhenLatestEqualsCurrent()
    {
        var m = new UpdateManifest("1.1.0", null, null, null, null, null);
        Assert.Null(UpdateDecision.Evaluate(Current, m));
    }

    [Fact]
    public void Evaluate_NoUpdate_WhenLatestOlder()
    {
        var m = new UpdateManifest("1.0.9", null, null, null, null, null);
        Assert.Null(UpdateDecision.Evaluate(Current, m));
    }

    [Fact]
    public void Evaluate_Update_WhenNewer()
    {
        var m = new UpdateManifest("1.2.0", null, "新功能", "https://x/y.msi", null, null);
        var rec = UpdateDecision.Evaluate(Current, m);
        Assert.NotNull(rec);
        Assert.Equal(new SemVersion(1, 1, 0), rec!.From);
        Assert.Equal(new SemVersion(1, 2, 0), rec.To);
        Assert.False(rec.IsMandatory);
        Assert.Equal("新功能", rec.Notes);
        Assert.Equal("https://x/y.msi", rec.DownloadUrl);
    }

    [Fact]
    public void Evaluate_ChannelMismatch_Skips()
    {
        var m = new UpdateManifest("1.2.0", "beta", null, null, null, null);
        Assert.Null(UpdateDecision.Evaluate(Current, m, channel: "stable"));
        // 不限定渠道时，显式渠道清单仍应被采纳
        Assert.NotNull(UpdateDecision.Evaluate(Current, m));
    }

    [Fact]
    public void Evaluate_Prerelease_SkippedUnlessIncluded()
    {
        var m = new UpdateManifest("1.2.0-rc.1", null, null, null, null, null);
        Assert.Null(UpdateDecision.Evaluate(Current, m, includePrerelease: false));
        Assert.NotNull(UpdateDecision.Evaluate(Current, m, includePrerelease: true));
    }

    [Fact]
    public void Evaluate_Mandatory_WhenBelowMinRequired()
    {
        var m = new UpdateManifest("1.2.0", null, null, null, "1.1.5", null);
        var rec = UpdateDecision.Evaluate(Current, m);
        Assert.NotNull(rec);
        Assert.True(rec!.IsMandatory);

        // 当前版本高于最低要求 → 非强制
        var m2 = new UpdateManifest("1.2.0", null, null, null, "1.0.0", null);
        Assert.False(UpdateDecision.Evaluate(Current, m2)!.IsMandatory);
    }

    [Fact]
    public void Evaluate_InvalidVersion_ReturnsNull()
    {
        var m = new UpdateManifest("not-a-version", null, null, null, null, null);
        Assert.Null(UpdateDecision.Evaluate(Current, m));
    }
}
