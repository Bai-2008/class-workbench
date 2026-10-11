namespace ClassScheduleApp.Services;

/// <summary>
/// 导出结果：成功时带回文件绝对路径，失败时带回原因。
/// </summary>
public sealed class ExportResult
{
    private ExportResult(bool success, string? filePath, string? errorMessage)
    {
        Success = success;
        FilePath = filePath;
        ErrorMessage = errorMessage;
    }

    /// <summary>是否导出成功。</summary>
    public bool Success { get; }

    /// <summary>导出文件的绝对路径（成功时有值）。</summary>
    public string? FilePath { get; }

    /// <summary>失败原因（失败时有值）。</summary>
    public string? ErrorMessage { get; }

    /// <summary>构造成功结果。</summary>
    public static ExportResult Ok(string filePath) => new(true, filePath, null);

    /// <summary>构造失败结果。</summary>
    public static ExportResult Fail(string errorMessage) => new(false, null, errorMessage);
}
