namespace ClassScheduleApp.Repositories;

/// <summary>
/// 班级空闲矩阵的一格：某天 + 某个节次对的空闲统计。
/// FreeStudentNames 供导出与详情使用；判定"有课"只依赖 StartWeek/EndWeek（WeekRange 已废弃）。
/// </summary>
public sealed class ClassFreeCell
{
    /// <summary>星期，1–7（1=周一）。</summary>
    public int DayOfWeek { get; set; }

    /// <summary>节次对下标，0–5（对应 1-2 / 3-4 / 5-6 / 7-8 / 9-10 / 11-12 节）。</summary>
    public int SlotIndex { get; set; }

    /// <summary>起始节次。</summary>
    public int StartPeriod { get; set; }

    /// <summary>结束节次。</summary>
    public int EndPeriod { get; set; }

    /// <summary>该时段空闲人数。</summary>
    public int FreeCount { get; set; }

    /// <summary>全班总人数（分母 = Students 表全部学生）。</summary>
    public int TotalCount { get; set; }

    /// <summary>该时段空闲学生姓名（排序与 GetFreeStudentsAsync 一致）。</summary>
    public IReadOnlyList<string> FreeStudentNames { get; set; } = Array.Empty<string>();

    /// <summary>是否全班都空闲。</summary>
    public bool IsAllFree => TotalCount > 0 && FreeCount == TotalCount;

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

    /// <summary>节次对的展示文本，如 1-2节。</summary>
    public string PeriodText => $"{StartPeriod}-{EndPeriod}节";
}
