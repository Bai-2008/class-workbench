namespace ClassScheduleApp.Models;

/// <summary>
/// 考勤记录实体，对应 Attendances 表（一表一类）。
/// 本表不设唯一约束；同一学生同一天同一场合是否已记录，由仓储的 UpsertBatchAsync 在应用层判定。
/// </summary>
public class Attendance
{
    /// <summary>主键。</summary>
    public int Id { get; set; }

    /// <summary>
    /// 关联学生的外键；删除学生时置空（SET NULL），历史考勤保留。
    /// 为 null 表示该学生已被删除，此时显示名读 <see cref="StudentName"/> 快照。
    /// </summary>
    public int? StudentId { get; set; }

    /// <summary>关联学生导航属性。</summary>
    public Student? Student { get; set; }

    /// <summary>
    /// 考勤发生时的学生姓名快照。
    /// 学生被删除后历史考勤仍能显示姓名；录入路径写入，删除学生前对该生缺失快照兜底回填。
    /// </summary>
    public string? StudentName { get; set; }

    /// <summary>考勤日期。</summary>
    public DateOnly Date { get; set; }

    /// <summary>关联课程的外键；null 表示非上课场合（如班级活动）。</summary>
    public int? CourseId { get; set; }

    /// <summary>关联课程导航属性。</summary>
    public Course? Course { get; set; }

    /// <summary>
    /// 考勤发生时的课程名快照（班级活动为 null）。
    /// 课程实体被删除后，历史考勤仍能显示当时上的是哪门课；历史查看一律优先读本字段。
    /// </summary>
    public string? CourseName { get; set; }

    /// <summary>考勤状态。</summary>
    public AttendanceStatus Status { get; set; }

    /// <summary>备注，可空。</summary>
    public string? Note { get; set; }

    /// <summary>记录创建时间（本地时间）。</summary>
    public DateTime CreatedAt { get; set; }
}
