namespace ClassScheduleApp.Models;

/// <summary>
/// 综测类别预设。汇总交叉表固定用这些类别做列，同时允许用户自定义输入其它类别。
/// </summary>
public static class ScoreCategories
{
    /// <summary>预设类别（顺序即交叉表的列顺序）。</summary>
    public static readonly string[] Presets =
    {
        "品德",
        "学业",
        "体育",
        "美育",
        "劳动实践",
        "社会工作",
        "竞赛获奖",
        "扣分",
    };
}
