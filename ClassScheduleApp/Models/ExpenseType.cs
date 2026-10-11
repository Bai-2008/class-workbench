namespace ClassScheduleApp.Models;

/// <summary>
/// 班费流水类型（数据库以整型存储）。
/// </summary>
public enum ExpenseType
{
    /// <summary>收入。</summary>
    Income = 0,

    /// <summary>支出。</summary>
    Expense = 1,
}
