using System.Collections.Generic;
using System.Reflection;
using Prismica.Core.Scheduling;
using Xunit;

namespace Prismica.Core.Tests.Scheduling;

public class EasingTests
{
    private static readonly (string In, string Out)[] InOutPairs =
    {
        ("EaseInQuad", "EaseOutQuad"),
        ("EaseInCubic", "EaseOutCubic"),
        ("EaseInQuart", "EaseOutQuart"),
        ("EaseInSine", "EaseOutSine"),
        ("EaseInCirc", "EaseOutCirc"),
        ("EaseInExpo", "EaseOutExpo"),
        ("EaseInBack", "EaseOutBack"),
        ("EaseInElastic", "EaseOutElastic"),
    };

    [Fact]
    public void AllNamedEasings_BoundaryZeroAndOne()
    {
        foreach (var name in NamedEasing.Names)
        {
            Assert.True(NamedEasing.TryResolve(name, out var fn), $"should resolve {name}");
            Assert.True(System.Math.Abs(fn(0)) < 1e-9, $"{name}(0) should be ~0, got {fn(0)}");
            Assert.True(System.Math.Abs(fn(1) - 1) < 1e-9, $"{name}(1) should be ~1, got {fn(1)}");
        }
    }

    [Fact]
    public void InOutPairs_AreSymmetric()
    {
        foreach (var (inName, outName) in InOutPairs)
        {
            Assert.True(NamedEasing.TryResolve(inName, out var fin));
            Assert.True(NamedEasing.TryResolve(outName, out var fout));
            for (double t = 0; t <= 1.0001; t += 0.1)
            {
                var sum = fin(t) + fout(1 - t);
                Assert.True(System.Math.Abs(sum - 1) < 1e-9,
                    $"{inName}({t}) + {outName}({1 - t}) = {sum}, expected ~1");
            }
        }
    }

    [Fact]
    public void EaseInQuad_IsMonotonicIncreasing()
    {
        Assert.True(NamedEasing.TryResolve("EaseInQuad", out var f));
        double prev = double.NegativeInfinity;
        for (double t = 0; t <= 1.0001; t += 0.05)
        {
            var v = f(t);
            Assert.True(v >= prev - 1e-12, $"not increasing at t={t}");
            prev = v;
        }
    }

    [Fact]
    public void EaseOutBounce_NonNegativeAndBounded()
    {
        Assert.True(NamedEasing.TryResolve("EaseOutBounce", out var f));
        for (double t = 0; t <= 1.0001; t += 0.02)
        {
            var v = f(t);
            Assert.True(v >= -1e-9 && v <= 1 + 1e-9, $"bounce out of range at t={t}: {v}");
        }
    }

    [Fact]
    public void UnknownName_FallsBackToLinear_ResolveOrDefault()
    {
        var fn = NamedEasing.ResolveOrDefault("does-not-exist");
        Assert.Equal(0.5, fn(0.5));
        Assert.False(NamedEasing.TryResolve("does-not-exist", out _));
    }

    [Fact]
    public void NamedEasing_CoversAllStaticEasingMethods()
    {
        var easingMethods = typeof(Easing).GetMethods(BindingFlags.Public | BindingFlags.Static);
        foreach (var m in easingMethods)
        {
            if (m.ReturnType == typeof(double) && m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType == typeof(double))
            {
                Assert.Contains(m.Name, NamedEasing.Names, System.StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
