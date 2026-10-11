namespace ClassScheduleApp.Repositories;

/// <summary>
/// 按学生汇总的考勤统计。
/// 出勤率 = (出勤 + 迟到) / 总数；无记录时出勤率为 0 且 <see cref="HasRecords"/> 为 false。
/// </summary>
public sealed class StudentAttendanceStats
{
    /// <summary>学生 Id。</summary>
    public int StudentId { get; set; }

    /// <summary>学号。</summary>
    public string StudentNo { get; set; } = string.Empty;

    /// <summary>姓名。</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>出勤次数。</summary>
    public int PresentCount { get; set; }

    /// <summary>迟到次数。</summary>
    public int LateCount { get; set; }

    /// <summary>旷课次数。</summary>
    public int AbsentCount { get; set; }

    /// <summary>请假次数。</summary>
    public int LeaveCount { get; set; }

    /// <summary>考核总次数（区间内该生的考勤记录数）。</summary>
    public int TotalCount => PresentCount + LateCount + AbsentCount + LeaveCount;

    /// <summary>区间内是否有考勤记录。</summary>
    public bool HasRecords => TotalCount > 0;

    /// <summary>出勤率，(出勤 + 迟到) / 总数；无记录时为 0。</summary>
    public double AttendanceRate => TotalCount == 0
        ? 0d
        : (double)(PresentCount + LateCount) / TotalCount;

    /// <summary>出勤率展示文本；无记录时显示"无记录"。</summary>
    public string AttendanceRateText => HasRecords
        ? AttendanceRate.ToString("P1")
        : "无记录";
}
