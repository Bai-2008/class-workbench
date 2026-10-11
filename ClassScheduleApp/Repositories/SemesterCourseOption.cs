namespace ClassScheduleApp.Repositories;

/// <summary>
/// 学期内可选课程（供调休补课弹窗的课程下拉使用）。
/// 注意：与 ViewModels 层既有的 CourseOption（选课窗口用）同名不同层，故此处以 Semester 前缀区分。
/// </summary>
public sealed class SemesterCourseOption
{
    public SemesterCourseOption(int id, string courseNo, string courseName, string teacher)
    {
        Id = id;
        CourseNo = courseNo;
        CourseName = courseName;
        Teacher = teacher;
    }

    /// <summary>课程 Id（唯一关联键）。</summary>
    public int Id { get; }

    /// <summary>课程编号（可能为空）。</summary>
    public string CourseNo { get; }

    /// <summary>课程名称。</summary>
    public string CourseName { get; }

    /// <summary>任课教师。</summary>
    public string Teacher { get; }

    /// <summary>下拉显示文本。</summary>
    public string DisplayText => CourseNo.Length > 0 ? $"{CourseName}（{CourseNo}）" : CourseName;

    /// <summary>供下拉读取显示文本（WPF 的项名称取 ToString，避免显示类型全名）。</summary>
    public override string ToString() => DisplayText;
}
