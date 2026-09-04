using System.Collections.Generic;
using Prismica.Core.Parameters;
using Prismica.Core.Primitives;
using Xunit;

namespace Prismica.Core.Tests.Parameters;

public class InterfaceBinderTests
{
    private static ComponentParameterSchema BuildSchema()
    {
        return new ComponentParameterSchema("Clock", new Dictionary<string, ParameterInfo>
        {
            ["FontColor"] = new ParameterInfo("FontColor", ParameterType.Color, "#FFFFFFFF", "字体色", null, null, null, null, null, true),
            ["BarColor"] = new ParameterInfo("BarColor", ParameterType.Color, "#FF00FF00", "进度色", null, null, null, null, null, false), // 无 ApplyTo => 隐式颜色
            ["Title"] = new ParameterInfo("Title", ParameterType.Text, "Hello", "标题", null, null, null, null, null, true),               // 隐式但非颜色 => 忽略
            ["Opacity"] = new ParameterInfo("Opacity", ParameterType.Color, "#FFFFFFFF", "透明度", null, null, null, null, "Root.Opacity", false), // 显式 ApplyTo => 忽略
        });
    }

    private static IReadOnlyDictionary<string, ArgbColor> BaseVars() =>
        new Dictionary<string, ArgbColor>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["FontColor"] = ArgbColor.FromHex("FFFFFFFF"),
            ["BaseOnly"] = ArgbColor.FromHex("00000000"),
        };

    [Fact]
    public void ResolveVariables_OverrideWinsOverBase()
    {
        var overrides = new Dictionary<string, object> { ["FontColor"] = "#FF0000", ["BarColor"] = "#00FF00" };
        var merged = InterfaceBinder.ResolveVariables(BuildSchema(), overrides, BaseVars());

        Assert.Equal(ArgbColor.FromHex("FF0000"), merged["FontColor"]);   // 实例覆盖优先
        Assert.Equal(ArgbColor.FromHex("00FF00"), merged["BarColor"]);   // 实例覆盖
        Assert.Equal(ArgbColor.FromHex("00000000"), merged["BaseOnly"]); // 基础变量保留
    }

    [Fact]
    public void ResolveVariables_NonColorImplicitAndApplyTo_AreIgnored()
    {
        var overrides = new Dictionary<string, object> { ["Title"] = "Ignored", ["Opacity"] = "#123456" };
        var merged = InterfaceBinder.ResolveVariables(BuildSchema(), overrides, BaseVars());

        // Title（非颜色隐式）/ Opacity（显式 ApplyTo）都不应进入颜色变量字典
        Assert.False(merged.ContainsKey("Title"));
        Assert.False(merged.ContainsKey("Opacity"));
        // 基础变量不受影响
        Assert.Equal(ArgbColor.FromHex("FFFFFFFF"), merged["FontColor"]);
    }

    [Fact]
    public void ResolveVariables_NoOverride_FallsBackToDefaultValue()
    {
        var merged = InterfaceBinder.ResolveVariables(BuildSchema(), new Dictionary<string, object>(), BaseVars());
        // BarColor 无 override 且基础变量也没有 => 取 DefaultValue #FF00FF00
        Assert.Equal(ArgbColor.FromHex("FF00FF00"), merged["BarColor"]);
    }

    [Fact]
    public void ResolveVariables_UnparseableOverride_FallsBackToBaseVariable()
    {
        var overrides = new Dictionary<string, object> { ["FontColor"] = "not-a-color" };
        var merged = InterfaceBinder.ResolveVariables(BuildSchema(), overrides, BaseVars());
        // 解析失败 => 回退基础变量
        Assert.Equal(ArgbColor.FromHex("FFFFFFFF"), merged["FontColor"]);
    }

    [Fact]
    public void ResolveVariables_NullSchema_Or_NullOverrides_ReturnsBase()
    {
        var baseVars = BaseVars();
        var a = InterfaceBinder.ResolveVariables(null, new Dictionary<string, object>(), baseVars);
        var b = InterfaceBinder.ResolveVariables(BuildSchema(), null, baseVars);
        Assert.Equal(baseVars["FontColor"], a["FontColor"]);
        Assert.Equal(baseVars["FontColor"], b["FontColor"]);
    }

    [Fact]
    public void TryParseColor_SupportsHexAndRgbAndRgba()
    {
        Assert.True(InterfaceBinder.TryParseColor("#FF0000", out var c1));
        Assert.Equal(ArgbColor.FromHex("FF0000"), c1);

        Assert.True(InterfaceBinder.TryParseColor("255,0,0", out var c2));
        Assert.Equal(ArgbColor.FromRgb(255, 0, 0), c2);

        Assert.True(InterfaceBinder.TryParseColor("255,0,0,128", out var c3));
        Assert.Equal(ArgbColor.FromRgba(255, 0, 0, 128), c3);

        Assert.False(InterfaceBinder.TryParseColor("garbage", out _));
        Assert.False(InterfaceBinder.TryParseColor("", out _));
    }
}
