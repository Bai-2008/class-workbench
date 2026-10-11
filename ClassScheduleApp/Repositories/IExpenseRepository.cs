using ClassScheduleApp.Models;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 班费记账的数据访问接口。
/// 余额不落库：由 <see cref="GetSummaryAsync"/> 按同一组过滤条件实时计算（总收入 − 总支出）。
/// </summary>
public interface IExpenseRepository
{
    /// <summary>
    /// 查询日期区间内的流水明细；类型 / 类别 / 关键词为可选过滤（传 null 或空表示不过滤）。
    /// 关键词匹配说明、类别与经办人。结果按日期升序。
    /// </summary>
    Task<IReadOnlyList<ExpenseRecordInfo>> GetEntriesAsync(
        DateOnly start,
        DateOnly end,
        ExpenseType? type = null,
        string? category = null,
        string? keyword = null,
        CancellationToken cancellationToken = default);

    /// <summary>按同一组过滤条件汇总：总收入、总支出、余额、笔数、按类别小计。</summary>
    Task<ExpenseSummary> GetSummaryAsync(
        DateOnly start,
        DateOnly end,
        ExpenseType? type = null,
        string? category = null,
        string? keyword = null,
        CancellationToken cancellationToken = default);

    /// <summary>新增一笔流水。</summary>
    Task<OperationResult> AddAsync(ExpenseInput input, CancellationToken cancellationToken = default);

    /// <summary>修改指定流水。</summary>
    Task<OperationResult> UpdateAsync(int id, ExpenseInput input, CancellationToken cancellationToken = default);

    /// <summary>删除指定流水。</summary>
    Task<OperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>类别下拉数据：预设类别在前，历史里出现过的自定义类别在后。</summary>
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>最近一次使用的经办人（用于编辑弹窗的默认值）；从未记账时返回 null。</summary>
    Task<string?> GetLastHandlerAsync(CancellationToken cancellationToken = default);
}
