using ClassScheduleApp.Models;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 班费流水明细（查询结果）：已组装好展示文本，供界面与导出直接使用。
/// </summary>
public sealed class ExpenseRecordInfo
{
    /// <summary>流水 Id。</summary>
    public int Id { get; set; }

    /// <summary>发生日期。</summary>
    public DateOnly Date { get; set; }

    /// <summary>类型：收入 / 支出。</summary>
    public ExpenseType Type { get; set; }

    /// <summary>金额（正数，方向由 Type 决定）。</summary>
    public decimal Amount { get; set; }

    /// <summary>类别。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>说明。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>经办人。</summary>
    public string Handler { get; set; } = string.Empty;

    /// <summary>备注。</summary>
    public string? Note { get; set; }

    /// <summary>日期展示文本。</summary>
    public string DateText => Date.ToString("yyyy-MM-dd");

    /// <summary>类型展示文本。</summary>
    public string TypeText => Type == ExpenseType.Income ? "收入" : "支出";

    /// <summary>带方向的金额展示文本（收入 +、支出 -）。</summary>
    public string AmountText => (Type == ExpenseType.Income ? "+" : "-") + Amount.ToString("N2");
}
