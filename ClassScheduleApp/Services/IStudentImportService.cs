namespace ClassScheduleApp.Services;

/// <summary>
/// 批量导入学生服务：解析 .xlsx / .csv（CSV 自动探测 UTF-8 / GBK 编码），
/// **以学号为身份键做纯增量导入**——已存在的学号一律跳过，不覆盖、不修改既有学生。
/// 全程本地解析，无网络请求。
/// </summary>
public interface IStudentImportService
{
    /// <summary>选择文件对话框用的过滤器。</summary>
    string FileFilter { get; }

    /// <summary>导入指定文件；返回报告（新增/跳过/失败逐行原因）。</summary>
    Task<StudentImportReport> ImportAsync(string filePath, CancellationToken cancellationToken = default);
}
