namespace ClassScheduleApp.Repositories;

/// <summary>
/// 班费汇总（同一组过滤条件下的口径）。余额不落库，等于总收入 − 总支出。
/// </summary>
public sealed class ExpenseSummary
{
    /// <summary>空汇总。</summary>
    public static ExpenseSummary Empty { get; } = new();

    /// <summary>总收入。</summary>
    public decimal TotalIncome { get; set; }

    /// <summary>总支出。</summary>
    public decimal TotalExpense { get; set; }

    /// <summary>当前余额 = 总收入 − 总支出。</summary>
    public decimal Balance => TotalIncome - TotalExpense;

    /// <summary>笔数。</summary>
    public int EntryCount { get; set; }

    /// <summary>按类别小计。</summary>
    public IReadOnlyList<ExpenseCategorySubtotal> SubTotals { get; set; } = new List<ExpenseCategorySubtotal>();
}
