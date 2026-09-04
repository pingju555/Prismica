using System.Threading;
using System.Threading.Tasks;
using Prismica.Core.Update;

namespace Prismica.App.Update;

/// <summary>更新源抽象：拉取最新更新清单。可换成 HTTP、本地文件、测试桩。</summary>
public interface IUpdateSource
{
    /// <summary>获取远端最新清单；无可用的清单（如网络失败、未配置）返回 null。</summary>
    Task<UpdateManifest?> GetLatestAsync(CancellationToken cancellationToken);
}
