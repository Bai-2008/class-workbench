namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 课表页顶部"每天日期(M/D)"表头的一项：星期名 + 该天的月/日，并标记今天（界面高亮）。
/// </summary>
public sealed class DayHeaderItem
{
    public DayHeaderItem(int dayOfWeek, DateOnly? date)
    {
        DayOfWeek = dayOfWeek;
        Date = date;
        IsToday = date.HasValue && date.Value == DateOnly.FromDateTime(DateTime.Today);
    }

    /// <summary>星期，1–7（1=周一）。</summary>
    public int DayOfWeek { get; }

    /// <summary>该天日期；学期未配置起始日时为 null。</summary>
    public DateOnly? Date { get; }

    /// <summary>是否今天（界面据此高亮）。</summary>
    public bool IsToday { get; }

    /// <summary>星期展示文本。</summary>
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

    /// <summary>月/日展示文本，如 10/6；无日期时为空。</summary>
    public string DateText => Date.HasValue ? $"{Date.Value.Month}/{Date.Value.Day}" : string.Empty;
}
