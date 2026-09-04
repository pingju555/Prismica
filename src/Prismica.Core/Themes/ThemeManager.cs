using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Prismica.Core.Primitives;

namespace Prismica.Core.Themes;

/// <summary>
/// 主题管理器：支持多主题切换，持久化用户选择。
/// </summary>
public sealed class ThemeManager
{
    private readonly string _themesDir;
    private readonly string _userPrefsPath;
    private readonly Dictionary<string, Theme> _themes = new();
    private string _currentThemeName = "Dark";

    public ThemeManager(string themesDir)
    {
        _themesDir = themesDir;
        Directory.CreateDirectory(_themesDir);
        _userPrefsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Prismica",
            "theme.json");
        LoadBuiltinThemes();
        LoadUserPreference();
    }

    /// <summary>当前主题。</summary>
    public Theme CurrentTheme => _themes.GetValueOrDefault(_currentThemeName) ?? _themes["Dark"];

    /// <summary>当前主题名称。</summary>
    public string CurrentThemeName => _currentThemeName;

    /// <summary>所有可用主题。</summary>
    public IReadOnlyDictionary<string, Theme> Themes => _themes;

    /// <summary>切换主题。</summary>
    public void SwitchTheme(string themeName)
    {
        if (_themes.ContainsKey(themeName))
        {
            _currentThemeName = themeName;
            SaveUserPreference();
            ThemeChanged?.Invoke(this, CurrentTheme);
        }
    }

    /// <summary>主题切换事件。</summary>
    public event EventHandler<Theme>? ThemeChanged;

    /// <summary>获取主题颜色值。</summary>
    public ArgbColor GetColor(string key)
    {
        return CurrentTheme.Colors.TryGetValue(key, out var color) ? color : new ArgbColor(0xFFFFFFFF);
    }

    /// <summary>获取主题颜色的 ARGB 字符串（#AARRGGBB）。</summary>
    public string GetColorHex(string key)
    {
        return CurrentTheme.Colors.TryGetValue(key, out var color) ? color.ToHex() : "#FFFFFFFF";
    }

    private void LoadBuiltinThemes()
    {
        _themes["Dark"] = new Theme("Dark", "深色主题", new Dictionary<string, ArgbColor>
        {
            ["Background"] = new ArgbColor(0xFF1E1E1E),
            ["Surface"] = new ArgbColor(0xFF2D2D2D),
            ["Card"] = new ArgbColor(0xFF3C3C3C),
            ["TextPrimary"] = new ArgbColor(0xFFFFFFFF),
            ["TextSecondary"] = new ArgbColor(0xFFB0B0B0),
            ["Accent"] = new ArgbColor(0xFF0078D4),
            ["AccentHover"] = new ArgbColor(0xFF1A8AE8),
            ["Success"] = new ArgbColor(0xFF4CAF50),
            ["Warning"] = new ArgbColor(0xFFFFC107),
            ["Error"] = new ArgbColor(0xFFF44336),
            ["Border"] = new ArgbColor(0xFF505050),
            ["Divider"] = new ArgbColor(0xFF404040)
        });

        _themes["Light"] = new Theme("Light", "浅色主题", new Dictionary<string, ArgbColor>
        {
            ["Background"] = new ArgbColor(0xFFF5F5F5),
            ["Surface"] = new ArgbColor(0xFFFFFFFF),
            ["Card"] = new ArgbColor(0xFFFFFFFF),
            ["TextPrimary"] = new ArgbColor(0xFF212121),
            ["TextSecondary"] = new ArgbColor(0xFF757575),
            ["Accent"] = new ArgbColor(0xFF1976D2),
            ["AccentHover"] = new ArgbColor(0xFF1565C0),
            ["Success"] = new ArgbColor(0xFF388E3C),
            ["Warning"] = new ArgbColor(0xFFF9A825),
            ["Error"] = new ArgbColor(0xFFD32F2F),
            ["Border"] = new ArgbColor(0xFFE0E0E0),
            ["Divider"] = new ArgbColor(0xFFEEEEEE)
        });

        _themes["Prismica"] = new Theme("Prismica", "Prismica 品牌主题", new Dictionary<string, ArgbColor>
        {
            ["Background"] = new ArgbColor(0xFF0D1117),
            ["Surface"] = new ArgbColor(0xFF161B22),
            ["Card"] = new ArgbColor(0xFF21262D),
            ["TextPrimary"] = new ArgbColor(0xFFF0F6FC),
            ["TextSecondary"] = new ArgbColor(0xFF8B949E),
            ["Accent"] = new ArgbColor(0xFF58A6FF),
            ["AccentHover"] = new ArgbColor(0xFF79C0FF),
            ["Success"] = new ArgbColor(0xFF3FB950),
            ["Warning"] = new ArgbColor(0xFFD29922),
            ["Error"] = new ArgbColor(0xFFF85149),
            ["Border"] = new ArgbColor(0xFF30363D),
            ["Divider"] = new ArgbColor(0xFF21262D)
        });

        // 加载自定义主题文件
        if (Directory.Exists(_themesDir))
        {
            foreach (var file in Directory.GetFiles(_themesDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var theme = JsonSerializer.Deserialize<Theme>(json);
                    if (theme is not null)
                        _themes[theme.Name] = theme;
                }
                catch { }
            }
        }
    }

    private void LoadUserPreference()
    {
        try
        {
            if (File.Exists(_userPrefsPath))
            {
                var json = File.ReadAllText(_userPrefsPath);
                var prefs = JsonSerializer.Deserialize<UserPrefs>(json);
                if (prefs?.Theme is not null && _themes.ContainsKey(prefs.Theme))
                    _currentThemeName = prefs.Theme;
            }
        }
        catch { }
    }

    private void SaveUserPreference()
    {
        try
        {
            var dir = Path.GetDirectoryName(_userPrefsPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            var prefs = new UserPrefs { Theme = _currentThemeName };
            var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_userPrefsPath, json);
        }
        catch { }
    }

    private sealed class UserPrefs
    {
        public string? Theme { get; set; }
    }
}

/// <summary>
/// 主题定义。
/// </summary>
public sealed record Theme(
    string Name,
    string Description,
    IReadOnlyDictionary<string, ArgbColor> Colors
);
