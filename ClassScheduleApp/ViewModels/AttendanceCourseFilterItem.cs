namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 统计页的课程过滤项：CourseId 为 null 表示"全部"（不按场合筛选）。
/// </summary>
public sealed class AttendanceCourseFilterItem
{
    public AttendanceCourseFilterItem(int? courseId, string displayText)
    {
        CourseId = courseId;
        DisplayText = displayText;
    }

    /// <summary>课程 Id；null = 全部。</summary>
    public int? CourseId { get; }

    /// <summary>下拉显示文本。</summary>
    public string DisplayText { get; }

    /// <summary>供下拉/辅助功能读取显示文本（WPF 的项名称取 ToString）。</summary>
    public override string ToString() => DisplayText;
}
