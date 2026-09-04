using System;
using System.IO;
using System.Threading.Tasks;
using Prismica.Core.CrashReporting;
using Xunit;

namespace Prismica.Core.Tests.CrashReporting;

public class CrashReportBuilderTests
{
    [Fact]
    public void Build_CapturesExceptionTypeAndMessage()
    {
        var ex = new InvalidOperationException("boom");
        var ctx = new CrashReportContext { AppVersion = "1.2.3" };

        var report = CrashReportBuilder.Build(ex, ctx);

        Assert.Equal("System.InvalidOperationException", report.ExceptionType);
        Assert.Equal("boom", report.ExceptionMessage);
        Assert.False(string.IsNullOrEmpty(report.TimestampUtc));
        Assert.Equal("1.2.3", report.AppVersion);
        Assert.Equal("Prismica", report.AppName);
    }

    [Fact]
    public void Build_CapturesInnerExceptionChain_OutermostFirst()
    {
        var ex = new Exception("outer",
            new ArgumentException("mid",
                new IOException("inner")));
        var ctx = new CrashReportContext { AppVersion = "0.1.0" };

        var report = CrashReportBuilder.Build(ex, ctx);

        Assert.Equal(2, report.InnerExceptions.Count);
        Assert.Equal("System.ArgumentException", report.InnerExceptions[0].ExceptionType);
        Assert.Equal("mid", report.InnerExceptions[0].Message);
        Assert.Equal("System.IO.IOException", report.InnerExceptions[1].ExceptionType);
        Assert.Equal("inner", report.InnerExceptions[1].Message);
    }

    [Fact]
    public void Build_UsesContextAppNameChannelAndCopiesAdditionalData()
    {
        var ex = new Exception("x");
        var ctx = new CrashReportContext
        {
            AppName = "MyApp",
            AppVersion = "9.9.9",
            BuildChannel = "beta",
            AdditionalData = new() { ["session"] = "abc", ["mode"] = "desktop" },
        };

        var report = CrashReportBuilder.Build(ex, ctx);

        Assert.Equal("MyApp", report.AppName);
        Assert.Equal("9.9.9", report.AppVersion);
        Assert.Equal("beta", report.BuildChannel);
        Assert.Equal("abc", report.AdditionalData["session"]);
        Assert.Equal("desktop", report.AdditionalData["mode"]);

        // 修改原字典不应影响已生成的报告（防御性复制）
        ctx.AdditionalData!["session"] = "tampered";
        Assert.Equal("abc", report.AdditionalData["session"]);
    }
}

public class CrashReportSerializationTests
{
    [Fact]
    public void ToJson_RoundTrip_PreservesFields()
    {
        var ex = new Exception("top", new InvalidOperationException("nested"));
        var report = CrashReportBuilder.Build(ex, new CrashReportContext
        {
            AppVersion = "2.0.0",
            BuildChannel = "stable",
            AdditionalData = new() { ["k"] = "v" },
        });

        var json = report.ToJson();
        var back = CrashReport.FromJson(json);

        Assert.NotNull(back);
        Assert.Equal(report.ExceptionType, back!.ExceptionType);
        Assert.Equal("top", back.ExceptionMessage);
        Assert.Equal("2.0.0", back.AppVersion);
        Assert.Equal("stable", back.BuildChannel);
        Assert.Equal("v", back.AdditionalData["k"]);
        Assert.Single(back.InnerExceptions);
        Assert.Equal("System.InvalidOperationException", back.InnerExceptions[0].ExceptionType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("{ not valid json")]
    public void FromJson_Invalid_ReturnsNull(string? input)
    {
        Assert.Null(CrashReport.FromJson(input!));
    }
}

public class LocalCrashSinkTests
{
    [Fact]
    public void WriteReport_WritesParseableJsonFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "prismica-crashtest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sink = new LocalCrashSink(dir);
            var report = CrashReportBuilder.Build(
                new NullReferenceException("npe"),
                new CrashReportContext { AppVersion = "1.0.0" });

            var path = sink.WriteReport(report);

            Assert.NotNull(path);
            Assert.True(File.Exists(path));
            var text = File.ReadAllText(path!);
            Assert.Contains("NullReferenceException", text);
            var parsed = CrashReport.FromJson(text);
            Assert.NotNull(parsed);
            Assert.Equal("npe", parsed!.ExceptionMessage);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WriteReport_NoOverwrite_WhenCalledTwiceQuickly()
    {
        var dir = Path.Combine(Path.GetTempPath(), "prismica-crashtest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sink = new LocalCrashSink(dir);
            var report = CrashReportBuilder.Build(new Exception("e"), new CrashReportContext { AppVersion = "1.0.0" });

            var p1 = sink.WriteReport(report);
            var p2 = sink.WriteReport(report);

            Assert.NotNull(p1);
            Assert.NotNull(p2);
            Assert.NotEqual(p1, p2);
            Assert.True(File.Exists(p1!));
            Assert.True(File.Exists(p2!));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WriteReport_NullDirectory_DoesNotThrow()
    {
        // 默认目录（%LOCALAPPDATA%）在本机存在；这里只验证调用不抛异常。
        var sink = new LocalCrashSink(null);
        var ex = Record.Exception(() =>
            sink.WriteReport(CrashReportBuilder.Build(new Exception("x"), new CrashReportContext { AppVersion = "1.0.0" })));
        Assert.Null(ex);
    }
}
