namespace ClassScheduleApp.Services.ScheduleImport;

/// <summary>
/// 课表导入服务：解析文件/文本 → 预览匹配 → 覆盖式导入（每文件独立事务）。
/// 全部本地完成，无任何网络请求。
/// </summary>
public interface IScheduleImportService
{
    /// <summary>
    /// 导入成功落地后触发（至少一个文件成功写入才触发）。
    /// 课表页、空闲统计等依赖学生课表的页面据此即时刷新，避免导入后仍显示旧数据。
    /// </summary>
    event EventHandler? Imported;

    /// <summary>读取并解析单个文件，同时按学号/姓名自动匹配学生。</summary>
    Task<ImportFilePreview> PreviewFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>解析粘贴的文本（不读文件），并按学号/姓名自动匹配学生。</summary>
    Task<ImportFilePreview> PreviewTextAsync(string content, string sourceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 覆盖式导入：对每个待导入项执行「解除该生本学期全部关联 → 按导入内容重建」。
    /// 每项独立事务，中途失败不回滚已完成的项。导入前先生成备份。
    /// </summary>
    Task<ScheduleImportReport> ImportAsync(
        IReadOnlyList<(ImportFilePreview Preview, int StudentId)> items,
        string semester,
        CancellationToken cancellationToken = default);
}
