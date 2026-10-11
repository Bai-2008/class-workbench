using ClassScheduleApp.Models;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 批量登记考勤时的一名学生条目（UpsertBatchAsync 的输入）。
/// </summary>
public sealed class AttendanceEntry
{
    public AttendanceEntry(int studentId, AttendanceStatus status, string? note)
    {
        StudentId = studentId;
        Status = status;
        Note = note;
    }

    /// <summary>学生 Id。</summary>
    public int StudentId { get; }

    /// <summary>考勤状态。</summary>
    public AttendanceStatus Status { get; }

    /// <summary>备注，可空。</summary>
    public string? Note { get; }
}
