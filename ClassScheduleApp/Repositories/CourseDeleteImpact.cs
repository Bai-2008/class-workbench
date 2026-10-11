namespace ClassScheduleApp.Repositories;

/// <summary>
/// 删除课程前的影响面（确认框逐项列明）。
/// </summary>
public sealed class CourseDeleteImpact
{
    /// <summary>关联学生数（选课关系条数）。</summary>
    public int StudentCount { get; set; }

    /// <summary>该课程的考勤记录条数（删除后**保留**，靠课程名快照呈现）。</summary>
    public int AttendanceCount { get; set; }

    /// <summary>涉及该课程的调休补课调整条数（删除时一并删除）。</summary>
    public int AdjustmentCount { get; set; }
}