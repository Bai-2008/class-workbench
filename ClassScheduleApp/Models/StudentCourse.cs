namespace ClassScheduleApp.Models;

/// <summary>
/// 学生选课实体，对应 StudentCourses 表：Students 与 Courses 的多对多中间表。
/// (StudentId, CourseId, Semester) 三元组唯一。
/// </summary>
public class StudentCourse
{
    /// <summary>主键。</summary>
    public int Id { get; set; }

    /// <summary>关联学生的外键。</summary>
    public int StudentId { get; set; }

    /// <summary>关联学生导航属性。</summary>
    public Student? Student { get; set; }

    /// <summary>关联课程的外键。</summary>
    public int CourseId { get; set; }

    /// <summary>关联课程导航属性。</summary>
    public Course? Course { get; set; }

    /// <summary>学期，如 2024-2025-1。</summary>
    public string Semester { get; set; } = string.Empty;
}
