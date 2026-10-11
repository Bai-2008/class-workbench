namespace ClassScheduleApp.Models;

/// <summary>
/// 考勤状态（数据库以整型存储）。
/// </summary>
public enum AttendanceStatus
{
    /// <summary>出勤。</summary>
    Present = 0,

    /// <summary>迟到。</summary>
    Late = 1,

    /// <summary>旷课。</summary>
    Absent = 2,

    /// <summary>请假。</summary>
    Leave = 3,
}
