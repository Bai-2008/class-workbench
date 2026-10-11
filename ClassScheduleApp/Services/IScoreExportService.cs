using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services;

/// <summary>
/// 综测汇总导出服务。
/// </summary>
public interface IScoreExportService
{
    /// <summary>
    /// 导出到 data/exports/综测汇总_{semester}.xlsx：
    /// Sheet1「汇总交叉表」（列与界面一致）、Sheet2「明细记录」（该学期全部记录）。
    /// </summary>
    Task<ExportResult> ExportAsync(
        string semester,
        IReadOnlyList<StudentScoreSummary> summaries,
        IReadOnlyList<ScoreEntryInfo> entries,
        CancellationToken cancellationToken = default);
}
