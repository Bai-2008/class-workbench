namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 网格单元格里的一门课（含调休叠加后的状态标记）。
/// </summary>
public sealed class WeekGridCourseEntry
{
    public WeekGridCourseEntry(int courseId, string courseName, string teacher, string location, bool isCancelled, bool isMakeUp)
    {
        CourseId = courseId;
        CourseName = courseName;
        Teacher = teacher;
        Location = location;
        IsCancelled = isCancelled;
        IsMakeUp = isMakeUp;
    }

    /// <summary>课程 Id。</summary>
    public int CourseId { get; }

    /// <summary>课程名。</summary>
    public string CourseName { get; }

    /// <summary>教师。</summary>
    public string Teacher { get; }

    /// <summary>教室（补课用调整里的新教室）。</summary>
    public string Location { get; }

    /// <summary>是否停课（原课程格保留课程名、弱化样式）。</summary>
    public bool IsCancelled { get; set; }

    /// <summary>是否补课（新增课程格）。</summary>
    public bool IsMakeUp { get; }

    /// <summary>标记文本：停课 / 补课 / 空。</summary>
    public string MarkText => IsCancelled ? "停课" : IsMakeUp ? "补课" : string.Empty;

    /// <summary>是否有标记。</summary>
    public bool HasMark => IsCancelled || IsMakeUp;

    /// <summary>单双周标记：单周 / 双周 / 空（每周为空）。</summary>
    public string ParityText { get; init; } = string.Empty;

    /// <summary>是否显示单双周小标。</summary>
    public bool HasParity => ParityText.Length > 0;
}
