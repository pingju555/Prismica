using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Prismica.Core.Update;

namespace Prismica.App.Update;

/// <summary>HTTP 更新源：从配置的 JSON URL 拉取 <see cref="UpdateManifest"/>。</summary>
public sealed class HttpUpdateSource : IUpdateSource
{
    private readonly HttpClient _http;
    private readonly string _url;

    public HttpUpdateSource(HttpClient http, string url)
    {
        _http = http;
        _url = url;
    }

    public async Task<UpdateManifest?> GetLatestAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_url)) return null;
        var json = await _http.GetStringAsync(_url, cancellationToken);
        return UpdateManifest.FromJson(json);
    }
}
