namespace ClassScheduleApp.Repositories;

/// <summary>
/// 某学期的一条已排课时段（考勤「某日期有效课程」计算的输入行）。
/// 周次与单双周由共享计算服务判定，本类型只承载数据。
/// </summary>
public sealed class CourseSlotRow
{
    public int CourseId { get; set; }

    public string CourseNo { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public string Teacher { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    /// <summary>星期 1–7。</summary>
    public int DayOfWeek { get; set; }

    public int StartPeriod { get; set; }

    public int EndPeriod { get; set; }

    public int StartWeek { get; set; }

    public int EndWeek { get; set; }

    public int WeekParity { get; set; }
}
