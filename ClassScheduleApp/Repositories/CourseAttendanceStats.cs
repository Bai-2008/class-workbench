namespace ClassScheduleApp.Repositories;

/// <summary>
/// 按课程/场合汇总的考勤统计（CourseId 为 null 表示班级活动等非上课场合）。
/// </summary>
public sealed class CourseAttendanceStats
{
    /// <summary>课程 Id；null 表示非上课场合。</summary>
    public int? CourseId { get; set; }

    /// <summary>场合名称：课程名或"班级活动"。</summary>
    public string OccasionName { get; set; } = string.Empty;

    /// <summary>出勤次数。</summary>
    public int PresentCount { get; set; }

    /// <summary>迟到次数。</summary>
    public int LateCount { get; set; }

    /// <summary>旷课次数。</summary>
    public int AbsentCount { get; set; }

    /// <summary>请假次数。</summary>
    public int LeaveCount { get; set; }

    /// <summary>记录总数。</summary>
    public int TotalCount => PresentCount + LateCount + AbsentCount + LeaveCount;

    /// <summary>出勤率，(出勤 + 迟到) / 总数。</summary>
    public double AttendanceRate => TotalCount == 0
        ? 0d
        : (double)(PresentCount + LateCount) / TotalCount;

    /// <summary>出勤率展示文本。</summary>
    public string AttendanceRateText => TotalCount == 0 ? "无记录" : AttendanceRate.ToString("P1");
}
