using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Prismica.App;
using Prismica.Core.Formula;
using Prismica.Core.Native;
using Prismica.Core.Rendering;
using Prismica.Core.Scheduling;
using Xunit;

namespace Prismica.Infra.Tests.App;

/// <summary>CompositionRoot DI 冒烟测试（WP4）：验证宿主可构建、核心服务可解析，不启动任何窗口�?/summary>
public class CompositionRootSmokeTests
{
    [Fact]
    public void BuildHost_ResolvesCoreServices()
    {
        var builder = CompositionRoot.CreateBuilder(Array.Empty<string>());
        using var host = builder.Build();

        using var scope = host.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<INativeDesktop>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRenderHost>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFormulaEngine>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFrameScheduler>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IOptions<DesktopOptions>>());
    }

    [Fact]
    public void BuildHost_RegistersDesktopHostedService()
    {
        // G2-R6 实测回归：之前漏注册 AddHostedService，导致 host.Run 不启动任何托管服务、覆盖窗口从不出现。
        var builder = CompositionRoot.CreateBuilder(Array.Empty<string>());
        using var host = builder.Build();

        var services = host.Services.GetServices<IHostedService>();
        Assert.Contains(services, s => s is DesktopHostedService);
    }
}
