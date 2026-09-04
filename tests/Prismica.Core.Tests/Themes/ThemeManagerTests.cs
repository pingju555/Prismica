using System;
using System.IO;
using Prismica.Core.Themes;
using Xunit;

namespace Prismica.Core.Tests.Themes;

public sealed class ThemeManagerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _prefsPath;

    public ThemeManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"prismica-theme-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _prefsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Prismica",
            "theme.json");
        // 清理用户偏好，确保测试隔离
        if (File.Exists(_prefsPath))
            File.Delete(_prefsPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
        if (File.Exists(_prefsPath))
            File.Delete(_prefsPath);
    }

    [Fact]
    public void Constructor_LoadsBuiltinThemes()
    {
        var manager = new ThemeManager(_testDir);
        Assert.Contains("Dark", manager.Themes);
        Assert.Contains("Light", manager.Themes);
        Assert.Contains("Prismica", manager.Themes);
        Assert.True(manager.Themes.Count >= 3);
    }

    [Fact]
    public void DefaultTheme_IsDark()
    {
        var manager = new ThemeManager(_testDir);
        Assert.Equal("Dark", manager.CurrentThemeName);
    }

    [Fact]
    public void SwitchTheme_ValidTheme_ChangesTheme()
    {
        var manager = new ThemeManager(_testDir);
        manager.SwitchTheme("Light");
        Assert.Equal("Light", manager.CurrentThemeName);
    }

    [Fact]
    public void SwitchTheme_InvalidTheme_NoChange()
    {
        var manager = new ThemeManager(_testDir);
        manager.SwitchTheme("NonExistent");
        Assert.Equal("Dark", manager.CurrentThemeName);
    }

    [Fact]
    public void ThemeChanged_Event_Fires()
    {
        var manager = new ThemeManager(_testDir);
        Theme? firedTheme = null;
        manager.ThemeChanged += (_, theme) => firedTheme = theme;

        manager.SwitchTheme("Light");

        Assert.NotNull(firedTheme);
        Assert.Equal("Light", firedTheme!.Name);
    }

    [Fact]
    public void GetColor_ReturnsColor()
    {
        var manager = new ThemeManager(_testDir);
        var color = manager.GetColor("Background");
        Assert.NotEqual(default, color);
    }

    [Fact]
    public void GetColorHex_ReturnsHex()
    {
        var manager = new ThemeManager(_testDir);
        var hex = manager.GetColorHex("Background");
        Assert.StartsWith("#", hex);
        Assert.Equal(9, hex.Length); // #AARRGGBB
    }

    [Fact]
    public void ThemeColors_ContainsRequiredKeys()
    {
        var manager = new ThemeManager(_testDir);
        foreach (var theme in manager.Themes.Values)
        {
            Assert.Contains("Background", theme.Colors.Keys);
            Assert.Contains("TextPrimary", theme.Colors.Keys);
            Assert.Contains("Accent", theme.Colors.Keys);
        }
    }

    [Fact]
    public void SwitchTheme_TwiceLastWins()
    {
        var manager = new ThemeManager(_testDir);
        manager.SwitchTheme("Light");
        manager.SwitchTheme("Prismica");
        Assert.Equal("Prismica", manager.CurrentThemeName);
    }
}
