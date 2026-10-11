namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 综测页的过滤下拉项（学生过滤与类别过滤共用）：
/// 学生过滤只填 <see cref="StudentId"/>（null = 全部学生）；类别过滤只填 <see cref="Category"/>（null = 全部类别）。
/// </summary>
public sealed class ScoreFilterOption
{
    public ScoreFilterOption(string displayText, int? studentId = null, string? category = null)
    {
        DisplayText = displayText;
        StudentId = studentId;
        Category = category;
    }

    /// <summary>下拉显示文本。</summary>
    public string DisplayText { get; }

    /// <summary>学生 Id；null 表示不限学生。</summary>
    public int? StudentId { get; }

    /// <summary>类别；null 表示不限类别。</summary>
    public string? Category { get; }

    /// <summary>供下拉与辅助功能读取显示文本（WPF 的项名称取 ToString）。</summary>
    public override string ToString() => DisplayText;
}
