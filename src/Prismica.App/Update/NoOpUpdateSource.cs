using System.Threading;
using System.Threading.Tasks;
using Prismica.Core.Update;

namespace Prismica.App.Update;

/// <summary>空更新源：未配置更新地址时使用，始终返回 null（即不检查更新）。</summary>
public sealed class NoOpUpdateSource : IUpdateSource
{
    public Task<UpdateManifest?> GetLatestAsync(CancellationToken cancellationToken)
        => Task.FromResult<UpdateManifest?>(null);
}
