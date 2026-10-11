namespace ClassScheduleApp.Repositories;

/// <summary>
/// 批量删除学生前的影响面（确认框逐项列明）。
/// </summary>
public sealed class StudentDeleteImpact
{
    /// <summary>将删除的学生数。</summary>
    public int StudentCount { get; set; }

    /// <summary>将一并解除的选课关联条数。</summary>
    public int EnrollmentCount { get; set; }

    /// <summary>该批学生的考勤记录条数（**保留**，靠学生名快照呈现）。</summary>
    public int AttendanceCount { get; set; }

    /// <summary>该批学生的综测记录条数（**保留**，靠学生名快照呈现）。</summary>
    public int ScoreEntryCount { get; set; }
}
