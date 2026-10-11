namespace ClassScheduleApp.Repositories;

/// <summary>
/// 按类别小计。
/// </summary>
public sealed class ExpenseCategorySubtotal
{
    /// <summary>类别。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>该类别的收入合计。</summary>
    public decimal IncomeAmount { get; set; }

    /// <summary>该类别的支出合计。</summary>
    public decimal ExpenseAmount { get; set; }

    /// <summary>该类别的笔数。</summary>
    public int EntryCount { get; set; }

    /// <summary>该类别净额 = 收入 − 支出。</summary>
    public decimal NetAmount => IncomeAmount - ExpenseAmount;

    /// <summary>净额展示文本（带正负号）。</summary>
    public string NetAmountText => NetAmount.ToString("N2");
}
