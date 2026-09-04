using System;

namespace Prismica.Infra.Native;

/// <summary>原生层调用失败的统一异常。</summary>
public sealed class NativeException : Exception
{
    public NativeException(int hresult, string message, string apiName)
        : base(message)
    {
        HResultValue = hresult;
        ApiName = apiName;
    }

    public int HResultValue { get; }
    public string ApiName { get; }
}
