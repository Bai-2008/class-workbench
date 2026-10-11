using ClassScheduleApp.Models;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 新增/修改班费流水的输入数据。
/// </summary>
public sealed class ExpenseInput
{
    public ExpenseInput(
        DateOnly date,
        ExpenseType type,
        decimal amount,
        string category,
        string description,
        string handler,
        string? note)
    {
        Date = date;
        Type = type;
        Amount = amount;
        Category = category;
        Description = description;
        Handler = handler;
        Note = note;
    }

    /// <summary>发生日期。</summary>
    public DateOnly Date { get; }

    /// <summary>类型。</summary>
    public ExpenseType Type { get; }

    /// <summary>金额（已四舍五入到两位小数、且大于 0）。</summary>
    public decimal Amount { get; }

    /// <summary>类别。</summary>
    public string Category { get; }

    /// <summary>说明。</summary>
    public string Description { get; }

    /// <summary>经办人。</summary>
    public string Handler { get; }

    /// <summary>备注。</summary>
    public string? Note { get; }
}
