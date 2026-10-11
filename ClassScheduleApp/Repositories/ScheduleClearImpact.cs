namespace ClassScheduleApp.Repositories;

/// <summary>
/// 「清空所有人的课表」确认框所需的全局计数：
/// 将删（关联 / 课程含时段 / 调整）与保留（学生 / 考勤记录）分开列明。
/// </summary>
public sealed class ScheduleClearImpact
{
    /// <summary>将删除：全部学生选课关联条数。</summary>
    public int EnrollmentCount { get; set; }

    /// <summary>将删除：课程门数（同时删除其全部时段）。</summary>
    public int CourseCount { get; set; }

    /// <summary>将删除：课程时段条数。</summary>
    public int ScheduleSlotCount { get; set; }

    /// <summary>将删除：调休补课调整条数。</summary>
    public int AdjustmentCount { get; set; }

    /// <summary>将保留：学生人数。</summary>
    public int StudentCount { get; set; }

    /// <summary>将保留：考勤记录条数（历史靠课程名快照呈现）。</summary>
    public int AttendanceCount { get; set; }
}