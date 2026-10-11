namespace ClassScheduleApp.Models;

/// <summary>
/// 班费流水实体，对应 Expenses 表（一表一类）。
/// 余额不落库，由总收入 − 总支出实时计算。
/// </summary>
public class Expense
{
    /// <summary>主键。</summary>
    public int Id { get; set; }

    /// <summary>发生日期。</summary>
    public DateOnly Date { get; set; }

    /// <summary>类型：收入 / 支出。</summary>
    public ExpenseType Type { get; set; }

    /// <summary>金额，必须大于 0（数据库有 CHECK 约束）。</summary>
    public decimal Amount { get; set; }

    /// <summary>类别：预设类别或自定义输入。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>说明，必填。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>经办人。</summary>
    public string Handler { get; set; } = string.Empty;

    /// <summary>备注，可空。</summary>
    public string? Note { get; set; }

    /// <summary>记录创建时间（本地时间）。</summary>
    public DateTime CreatedAt { get; set; }
}
