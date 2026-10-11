namespace ClassScheduleApp.Models;

/// <summary>
/// 学生实体，对应 Students 表（一表一类）。
/// </summary>
public class Student
{
    /// <summary>主键。</summary>
    public int Id { get; set; }

    /// <summary>学号，唯一索引。</summary>
    public string StudentNo { get; set; } = string.Empty;

    /// <summary>姓名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>性别。</summary>
    public string Gender { get; set; } = string.Empty;

    /// <summary>班级名称。</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>联系电话，可空。</summary>
    public string? Phone { get; set; }

    /// <summary>备注，可空。</summary>
    public string? Note { get; set; }

    /// <summary>选课记录（与 Courses 的多对多中间表导航）。</summary>
    public ICollection<StudentCourse> StudentCourses { get; set; } = new List<StudentCourse>();
}
