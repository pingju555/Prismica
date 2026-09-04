using Prismica.Core.Actions;
using Prismica.Core.Formula;
using Xunit;
using FluentAssertions;

namespace Prismica.Core.Tests.Actions;

public class DefaultActionRunnerTests
{
    private readonly DefaultActionRunner _runner = new();
    private readonly DefaultFormulaEngine _engine = new();

    private ActionContext CreateContext()
    {
        return new ActionContext(
            new Dictionary<string, Prismica.Core.Measures.IMeasure>(),
            new Dictionary<string, Prismica.Core.Meters.IMeter>(),
            new Dictionary<string, Prismica.Core.Components.IEmbedHost>(),
            new Dictionary<string, Prismica.Core.Primitives.ArgbColor>(),
            _engine,
            MockNativeDesktop.Instance,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task ExecuteAsync_SetVariable_Works()
    {
        var action = new ActionDefinition(
            ActionKind.SetVariable,
            new Dictionary<string, object> { ["Name"] = "testVar", ["Value"] = 42 },
            null
        );
        var ctx = CreateContext();
        var result = await _runner.ExecuteAsync(action, ctx);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_Delay_Works()
    {
        var action = new ActionDefinition(
            ActionKind.Delay,
            new Dictionary<string, object> { ["Ms"] = 10 },
            null
        );
        var ctx = CreateContext();
        var start = DateTime.UtcNow;
        var result = await _runner.ExecuteAsync(action, ctx);
        result.Success.Should().BeTrue();
        (DateTime.UtcNow - start).TotalMilliseconds.Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public async Task ExecuteAsync_Condition_Skips_When_False()
    {
        var action = new ActionDefinition(
            ActionKind.SetVariable,
            new Dictionary<string, object> { ["Name"] = "x", ["Value"] = 1 },
            "false"
        );
        var ctx = CreateContext();
        var result = await _runner.ExecuteAsync(action, ctx);
        result.Success.Should().BeTrue();
        result.Output.Should().Be("skipped");
    }

    [Fact]
    public async Task RunFlowAsync_Sequential_Steps_Work()
    {
        var flow = new FlowDefinition("test", new List<FlowStep>
        {
            new(new ActionDefinition(ActionKind.Delay, new Dictionary<string, object> { ["Ms"] = 5 }, null), null, null),
            new(new ActionDefinition(ActionKind.Delay, new Dictionary<string, object> { ["Ms"] = 5 }, null), null, null)
        });
        var ctx = CreateContext();
        var result = await _runner.RunFlowAsync(flow, ctx);
        result.Completed.Should().BeTrue();
        result.StepsExecuted.Should().Be(2);
    }

    [Fact]
    public async Task RunFlowAsync_Condition_Skips_Step()
    {
        var flow = new FlowDefinition("test", new List<FlowStep>
        {
            new(new ActionDefinition(ActionKind.Delay, new Dictionary<string, object> { ["Ms"] = 1 }, null), "false", null),
            new(new ActionDefinition(ActionKind.Delay, new Dictionary<string, object> { ["Ms"] = 1 }, null), null, null)
        });
        var ctx = CreateContext();
        var result = await _runner.RunFlowAsync(flow, ctx);
        result.Completed.Should().BeTrue();
        result.StepsExecuted.Should().Be(1);
    }

    [Fact]
    public async Task RunFlowAsync_Loop_Works()
    {
        // 循环回到索引 1
        var flow = new FlowDefinition("test", new List<FlowStep>
        {
            new(new ActionDefinition(ActionKind.SetVariable, new Dictionary<string, object> { ["Name"] = "i", ["Value"] = 0 }, null), null, null),
            new(new ActionDefinition(ActionKind.SetVariable, new Dictionary<string, object> { ["Name"] = "i", ["Value"] = 1 }, null), null, 1) // 循环回到索引 1
        });
        var ctx = CreateContext();
        var result = await _runner.RunFlowAsync(flow, ctx);
        result.Completed.Should().BeTrue();
    }

    private sealed class MockNativeDesktop : Prismica.Core.Native.INativeDesktop
    {
        public static MockNativeDesktop Instance { get; } = new();
        public Prismica.Core.Native.IOverlayWindow CreateOverlayWindow(Prismica.Core.Native.ScreenInfo screen) => throw new NotImplementedException();
        public void SetClickThrough(IntPtr hwnd, bool enable) { }
        public IDisposable TrackDesktopIcons(Action<Prismica.Core.Native.IconChangeEvent> onChange) => new NopDisposable();
        public IDisposable WatchFileSystem(string path, Prismica.Core.Native.FileSystemWatcherOptions opts, Action<Prismica.Core.Native.FileChangeEvent> onChange) => new NopDisposable();
        public Task ExecuteVerbAsync(string filePath, string verb, IntPtr ownerHwnd) => Task.CompletedTask;
        public Task<Prismica.Core.Native.IconData> GetIconAsync(string path, Prismica.Core.Native.IconSize size, bool thumbnail) => Task.FromResult(new Prismica.Core.Native.IconData(Array.Empty<Prismica.Core.Primitives.ArgbColor>(), 0, 0, false));
        public IReadOnlyList<Prismica.Core.Native.DesktopIconItem> GetDesktopIcons() => Array.Empty<Prismica.Core.Native.DesktopIconItem>();
#pragma warning disable CS0067 // 接口必需的 mock 事件，未使用
        public event Action<Prismica.Core.Native.ScreenLayoutChanged>? ScreenLayoutChanged;
#pragma warning restore CS0067
        public void Dispose() { }

        private sealed class NopDisposable : IDisposable { public void Dispose() { } }
    }
}