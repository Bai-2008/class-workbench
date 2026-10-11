using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services;

/// <summary>
/// 班费账单导出服务。
/// </summary>
public interface IExpenseExportService
{
    /// <summary>
    /// 导出到 data/exports/班费账单_{start}_{end}.xlsx：
    /// Sheet1「汇总」（总收入/总支出/当前余额/笔数 + 按类别小计表）、Sheet2「明细流水」（日期升序）。
    /// </summary>
    Task<ExportResult> ExportAsync(
        DateOnly start,
        DateOnly end,
        ExpenseSummary summary,
        IReadOnlyList<ExpenseRecordInfo> entries,
        CancellationToken cancellationToken = default);
}
