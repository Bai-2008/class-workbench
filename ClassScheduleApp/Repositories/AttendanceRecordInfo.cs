using ClassScheduleApp.Models;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 考勤明细记录（查询结果）：已把学生与课程名组装好，供界面与导出直接使用。
/// </summary>
public sealed class AttendanceRecordInfo
{
    /// <summary>考勤记录 Id。</summary>
    public int Id { get; set; }

    /// <summary>学生 Id；学生被删除后为 null（此时姓名读快照）。</summary>
    public int? StudentId { get; set; }

    /// <summary>学号。</summary>
    public string StudentNo { get; set; } = string.Empty;

    /// <summary>学生姓名。</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>考勤日期。</summary>
    public DateOnly Date { get; set; }

    /// <summary>课程 Id；null 表示非上课场合。</summary>
    public int? CourseId { get; set; }

    /// <summary>课程名；班级活动等非上课场合为 null。</summary>
    public string? CourseName { get; set; }

    /// <summary>考勤状态。</summary>
    public AttendanceStatus Status { get; set; }

    /// <summary>备注。</summary>
    public string? Note { get; set; }

    /// <summary>场合展示文本：课程名或"班级活动"。</summary>
    public string OccasionText => string.IsNullOrEmpty(CourseName) ? "班级活动" : CourseName;

    /// <summary>状态展示文本。</summary>
    public string StatusText => Status switch
    {
        AttendanceStatus.Present => "出勤",
        AttendanceStatus.Late => "迟到",
        AttendanceStatus.Absent => "旷课",
        AttendanceStatus.Leave => "请假",
        _ => $"未知({(int)Status})",
    };
}
