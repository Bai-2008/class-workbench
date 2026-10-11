namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 考勤登记的"场合"下拉项：CourseId 为 null 表示非上课场合（班级活动）。
/// </summary>
public sealed class AttendanceOccasionItem
{
    public AttendanceOccasionItem(int? courseId, string displayText)
    {
        CourseId = courseId;
        DisplayText = displayText;
    }

    /// <summary>课程 Id；null = 班级活动。</summary>
    public int? CourseId { get; }

    /// <summary>下拉显示文本。</summary>
    public string DisplayText { get; }

    /// <summary>供下拉/辅助功能读取显示文本（WPF 的项名称取 ToString）。</summary>
    public override string ToString() => DisplayText;
}
