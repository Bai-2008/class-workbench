using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services;

/// <summary>
/// 导出服务：把统计结果写成文件供外部打开。
/// </summary>
public interface IExportService
{
    /// <summary>
    /// 导出本周无课表到 data/exports/无课表_{semester}_第{week}周.xlsx。
    /// 单工作表、7 列（周一~周日）× 6 行（节次对）；每格填空闲学生姓名并自动换行，
    /// 全班空闲的格填"全班空闲"。
    /// </summary>
    Task<ExportResult> ExportFreeTimeGridAsync(
        string semester,
        int week,
        IReadOnlyList<ClassFreeCell> cells,
        CancellationToken cancellationToken = default);
}
