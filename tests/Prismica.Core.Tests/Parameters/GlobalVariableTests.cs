using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Prismica.Core.Components;
using Prismica.Core.Measures;
using Prismica.Core.Meters;
using Prismica.Core.Parsing;
using Prismica.Core.Parameters;
using Prismica.Core.Primitives;
using Xunit;

namespace Prismica.Core.Tests.Parameters;

/// <summary>
/// F9 跨组件全局变量（gv:）测试：存储 API、.pri 解析、StringMeter #gv:Name# 替换与跨实例共享。
/// </summary>
public class GlobalVariableTests
{
    [Fact]
    public void Store_SetGet_TryAdd_Remove_Clear_Work()
    {
        var store = new GlobalVariableStore();
        Assert.Null(store.Get("X"));

        store.Set("X", "1");
        Assert.Equal("1", store.Get("X"));

        // TryAdd 仅当不存在时写入
        Assert.False(store.TryAdd("X", "99"));
        Assert.Equal("1", store.Get("X"));
        Assert.True(store.TryAdd("Y", "2"));
        Assert.Equal("2", store.Get("Y"));

        // 大小写不敏感键
        store.Set("Theme", "Dark");
        Assert.Equal("Dark", store.Get("theme"));

        Assert.True(store.Remove("X"));
        Assert.Null(store.Get("X"));

        var snap = store.Snapshot();
        Assert.Equal("Dark", snap["Theme"]);

        store.Clear();
        Assert.Empty(store);
    }

    [Fact]
    public void Parser_ExtractsGlobalVariables_FromSection()
    {
        var pri = @"[Prismica]
Name=GvTest
[GlobalVariables]
Theme=Dark
Accent=#FF4C8BF5
[Variables]
FontColor=#FFFFFFFF
[MeterTitle]
Meter=String
Text=#gv:Theme#";
        var result = new IniSkinTextParser().Parse(pri);
        Assert.True(result.Success);
        Assert.NotNull(result.Definition);
        Assert.Equal("Dark", result.Definition!.GlobalVariables["Theme"]);
        Assert.Equal("#FF4C8BF5", result.Definition.GlobalVariables["Accent"]);
        // [Variables] 与 [GlobalVariables] 互不干扰
        Assert.Equal((ArgbColor)0xFFFFFFFF, result.Definition.Variables["FontColor"]);
    }

    private static MeterContext MakeCtx(IReadOnlyDictionary<string, string> globals)
        => new(
            new Dictionary<string, IMeasure>(),
            new Dictionary<string, ArgbColor>(),
            null!,
            new Rect(0, 0, 400, 300),
            TimeSpan.Zero,
            globals);

    [Fact]
    public async Task StringMeter_GvSubstitution_ReadsSharedStore()
    {
        var store = new GlobalVariableStore();
        store.Set("Theme", "Dark");

        var meter = new StringMeter("m");
        meter.Configure(new Dictionary<string, string>
        {
            ["X"] = "0", ["Y"] = "0", ["W"] = "200", ["H"] = "40",
            ["Text"] = "#gv:Theme#"
        });
        await meter.UpdateAsync(MakeCtx(store));
        Assert.Equal("Dark", meter.RenderedText);
    }

    [Fact]
    public async Task StringMeter_GvAndVar_BothSubstitute()
    {
        var ctx = MakeCtx(new Dictionary<string, string> { ["Theme"] = "Dark" });
        var meter = new StringMeter("m");
        meter.Configure(new Dictionary<string, string>
        {
            ["X"] = "0", ["Y"] = "0", ["W"] = "200", ["H"] = "40",
            ["Text"] = "#Var# #gv:Theme#"
        });
        // 注意：Variables 为空时 #Var# 不被替换；此处演示两者机制并存。
        await meter.UpdateAsync(ctx);
        Assert.Equal("#Var# Dark", meter.RenderedText);
    }

    [Fact]
    public async Task StringMeter_CrossInstance_SharesLiveStore()
    {
        // 两个组件（meter）共享同一 GlobalVariableStore 实例：组件 A 写入、组件 B 读取实时联动。
        var store = new GlobalVariableStore();

        var a = new StringMeter("a");
        a.Configure(new Dictionary<string, string> { ["X"] = "0", ["Y"] = "0", ["W"] = "100", ["H"] = "30", ["Text"] = "writer" });
        var b = new StringMeter("b");
        b.Configure(new Dictionary<string, string> { ["X"] = "0", ["Y"] = "40", ["W"] = "100", ["H"] = "30", ["Text"] = "#gv:Theme#" });

        // 初始：全局变量尚不存在 → #gv:Theme# 原样保留
        await a.UpdateAsync(MakeCtx(store));
        await b.UpdateAsync(MakeCtx(store));
        Assert.Equal("#gv:Theme#", b.RenderedText);

        // 组件 A 写入全局变量（运行期 Set，模拟 gv:Theme=Dark）
        store.Set("Theme", "Dark");

        // 下一帧：组件 B 经 #gv:Theme# 实时反映最新值
        await a.UpdateAsync(MakeCtx(store));
        await b.UpdateAsync(MakeCtx(store));
        Assert.Equal("Dark", b.RenderedText);
    }
}
