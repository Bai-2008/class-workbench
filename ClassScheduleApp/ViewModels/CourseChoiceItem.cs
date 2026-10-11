namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 课表冲突时候选列表里的一项（同一格子的多门课之一）。
/// </summary>
public sealed class CourseChoiceItem
{
    public CourseChoiceItem(int courseId, string courseName, string teacher, string location, string timeText)
    {
        CourseId = courseId;
        CourseName = courseName;
        Teacher = teacher;
        Location = location;
        TimeText = timeText;
    }

    /// <summary>课程 Id（用于打开既有课程编辑弹窗，禁止用课程名做关联）。</summary>
    public int CourseId { get; }

    /// <summary>课程名。</summary>
    public string CourseName { get; }

    /// <summary>任课教师。</summary>
    public string Teacher { get; }

    /// <summary>教室。</summary>
    public string Location { get; }

    /// <summary>节次时间文本。</summary>
    public string TimeText { get; }

    /// <summary>列表显示文本。</summary>
    public string DisplayText => $"{CourseName}    {Teacher}    {Location}    {TimeText}";

    /// <summary>供下拉读取显示文本（WPF 的项名称取 ToString，避免显示类型全名）。</summary>
    public override string ToString() => DisplayText;
}
