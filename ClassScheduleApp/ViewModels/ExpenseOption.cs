using ClassScheduleApp.Models;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 班费模块的下拉项。两种用法：
/// ① 类型下拉：只填 <see cref="Type"/>（编辑弹窗用），或再填"全部"（过滤栏用，Type 为 null 表示全部）；
/// ② 类别下拉：只填 <see cref="Category"/>（null 表示全部类别）。
/// </summary>
public sealed class ExpenseOption
{
    public ExpenseOption(string displayText, ExpenseType? type = null, string? category = null)
    {
        DisplayText = displayText;
        Type = type;
        Category = category;
    }

    /// <summary>下拉显示文本。</summary>
    public string DisplayText { get; }

    /// <summary>类型过滤值；null 表示不限类型。</summary>
    public ExpenseType? Type { get; }

    /// <summary>类别过滤值；null 表示不限类别。</summary>
    public string? Category { get; }

    /// <summary>供下拉与辅助功能读取显示文本（WPF 的项名称取 ToString）。</summary>
    public override string ToString() => DisplayText;
}
