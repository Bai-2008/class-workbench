namespace ClassScheduleApp.Repositories;

/// <summary>
/// 一条上课时段（课程编辑弹窗里的每一行，也是学生课表的过滤单元）。
/// </summary>
public sealed class CourseScheduleDetail
{
    /// <summary>排课主键；0 表示该时段尚未落库。</summary>
    public int Id { get; set; }

    /// <summary>星期，1–7（1=周一）。</summary>
    public int DayOfWeek { get; set; }

    /// <summary>起始节次。</summary>
    public int StartPeriod { get; set; }

    /// <summary>结束节次。</summary>
    public int EndPeriod { get; set; }

    /// <summary>起始周。</summary>
    public int StartWeek { get; set; } = 1;

    /// <summary>结束周。</summary>
    public int EndWeek { get; set; } = 20;

    /// <summary>上课教室。</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>单双周：0 每周 / 1 单周 / 2 双周（见 <see cref="Models.WeekParityTypes"/>）。</summary>
    public int WeekParity { get; set; }

    /// <summary>单双周展示文本；每周时为空串（界面据此决定是否加小标）。</summary>
    public string ParityText => WeekParity == Models.WeekParityTypes.Every
        ? string.Empty
        : Models.WeekParityTypes.ToDisplay(WeekParity);

    /// <summary>星期的展示文本。</summary>
    public string WeekdayText => DayOfWeek switch
    {
        1 => "周一",
        2 => "周二",
        3 => "周三",
        4 => "周四",
        5 => "周五",
        6 => "周六",
        7 => "周日",
        _ => $"星期{DayOfWeek}",
    };

    /// <summary>节次的展示文本，如 1-2节。</summary>
    public string PeriodText => $"{StartPeriod}-{EndPeriod}节";

    /// <summary>周次的展示文本，如 1-16周。</summary>
    public string WeekRangeText => $"{StartWeek}-{EndWeek}周";
}
