namespace ClassScheduleApp.Models;

/// <summary>
/// 班费类别预设。下拉里始终提供这些预设，同时允许用户自定义输入。
/// </summary>
public static class ExpenseCategories
{
    /// <summary>预设类别（顺序即下拉里的显示顺序）。</summary>
    public static readonly string[] Presets =
    {
        "班费收取",
        "班级活动",
        "用品采购",
        "竞赛经费",
        "礼品慰问",
        "其他",
    };
}
