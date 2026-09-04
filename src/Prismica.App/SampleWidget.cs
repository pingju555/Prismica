using System.Collections.Generic;
using Prismica.Core.Components;
using Prismica.Core.Parameters;
using Prismica.Core.Primitives;

namespace Prismica.App;

/// <summary>原型演示小部件：一个简单的 Label 组件定义。</summary>
public static class SampleWidget
{
    public static ComponentDefinition Create()
    {
        var meter = new MeterDefinition("clock", "label", new Dictionary<string, string>
        {
            ["X"] = "0", ["Y"] = "0", ["W"] = "240", ["H"] = "80",
            ["Text"] = "Prismica", ["Color"] = "#22FFFFFF", ["FontSize"] = "42"
        });
        return new ComponentDefinition(
            "sample", "0.1", new PrismicaSection("0.1", "sample", "prismica", "demo", 4, 30, 240, 80),
            new Dictionary<string, ArgbColor>(),
            new Dictionary<string, string>(),
            new ComponentParameterSchema("sample", new Dictionary<string, ParameterInfo>()),
            new List<MeasureDefinition>(),
            new List<MeterDefinition> { meter },
            new List<EmbedDefinition>(),
            new List<StyleDefinition>(),
            new List<AnimationSpec>());
    }
}
