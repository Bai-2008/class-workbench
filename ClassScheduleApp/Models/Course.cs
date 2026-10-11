namespace ClassScheduleApp.Models;

/// <summary>
/// 课程实体，对应 Courses 表（一表一类）。
/// </summary>
public class Course
{
    /// <summary>主键。</summary>
    public int Id { get; set; }

    /// <summary>课程编号，唯一索引（编号已知时防重）。<b>选填</b>：留空存 NULL，SQLite 唯一索引允许多个 NULL。</summary>
    public string? CourseNo { get; set; }

    /// <summary>课程名称。</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>任课教师。</summary>
    public string Teacher { get; set; } = string.Empty;

    /// <summary>
    /// 学分。<b>选填</b>：留空存 NULL。
    /// 使用 double 以匹配 SQLite 的 REAL，避免 decimal 在 SQLite 上的存储告警。
    /// </summary>
    public double? Credit { get; set; }

    /// <summary>排课记录。</summary>
    public ICollection<CourseSchedule> CourseSchedules { get; set; } = new List<CourseSchedule>();

    /// <summary>选课记录（与 Students 的多对多中间表导航）。</summary>
    public ICollection<StudentCourse> StudentCourses { get; set; } = new List<StudentCourse>();
}
