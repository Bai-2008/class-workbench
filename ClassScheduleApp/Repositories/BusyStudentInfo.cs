namespace ClassScheduleApp.Repositories;

/// <summary>
/// 某时段"有课"的一条记录：哪个学生在什么时间上什么课。
/// </summary>
public sealed class BusyStudentInfo
{
    /// <summary>学生 Id。</summary>
    public int StudentId { get; set; }

    /// <summary>学号。</summary>
    public string StudentNo { get; set; } = string.Empty;

    /// <summary>学生姓名。</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>课程名称。</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>上课教室。</summary>
    public string Location { get; set; } = string.Empty;
}
