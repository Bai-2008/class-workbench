namespace ClassScheduleApp.Repositories;

/// <summary>
/// 数据写操作的统一结果：成功 / 失败原因。
/// 仓储内部消化数据库异常，向上层返回友好文案，避免把 EF 异常泄漏到 ViewModel。
/// </summary>
public sealed class OperationResult
{
    private OperationResult(bool success, string? errorMessage)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    /// <summary>是否成功。</summary>
    public bool Success { get; }

    /// <summary>失败原因（成功时为 null）。</summary>
    public string? ErrorMessage { get; }

    /// <summary>构造成功结果。</summary>
    public static OperationResult Ok() => new(true, null);

    /// <summary>构造失败结果。</summary>
    public static OperationResult Fail(string errorMessage) => new(false, errorMessage);
}
