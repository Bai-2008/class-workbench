namespace ClassScheduleApp.Repositories;

/// <summary>
/// 某学期被引用的数据量（删除学期前的知情提示用）。
/// </summary>
public sealed class SemesterUsage
{
    /// <summary>该学期的学生选课（课表）条数。</summary>
    public int EnrollmentCount { get; set; }

    /// <summary>该学期的调休补课调整条数。</summary>
    public int AdjustmentCount { get; set; }

    /// <summary>是否还有其它数据引用该学期。</summary>
    public bool HasData => EnrollmentCount > 0 || AdjustmentCount > 0;
}
