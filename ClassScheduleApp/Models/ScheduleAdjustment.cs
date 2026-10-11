namespace ClassScheduleApp.Models;

/// <summary>
/// 调休补课调整记录，对应 ScheduleAdjustments 表。
/// 「调课」不单独建类型，由「停课 + 补课」两条记录组成（弹窗一次事务生成）。
/// </summary>
public class ScheduleAdjustment
{
    /// <summary>主键。</summary>
    public int Id { get; set; }

    /// <summary>学期（与 StudentCourses.Semester 一致）。</summary>
    public string Semester { get; set; } = string.Empty;

    /// <summary>调整类型，取值见 <see cref="ScheduleAdjustmentTypes"/>。</summary>
    public int AdjustmentType { get; set; }

    /// <summary>课程 Id；停课/补课必填，放假/调休整天类型不填。</summary>
    public int? CourseId { get; set; }

    /// <summary>调整落在的日历日。</summary>
    public DateOnly AffectedDate { get; set; }

    /// <summary>起始节次；节次级类型（停课/补课/调课）必填。</summary>
    public int? StartPeriod { get; set; }

    /// <summary>结束节次；节次级类型必填。</summary>
    public int? EndPeriod { get; set; }

    /// <summary>仅调休上课日使用：该日按周几的课表上课（1–7）。</summary>
    public int? FollowWeekday { get; set; }

    /// <summary>补课教室（可空）。</summary>
    public string? NewLocation { get; set; }

    /// <summary>备注（可空）。</summary>
    public string? Note { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 调整类型常量与展示文本（视图与仓储共用，避免各处硬编码数字）。
/// </summary>
public static class ScheduleAdjustmentTypes
{
    /// <summary>停课。</summary>
    public const int Cancelled = 0;

    /// <summary>补课。</summary>
    public const int MakeUp = 1;

    /// <summary>放假（整天，支持日期区间按天展开）。</summary>
    public const int Holiday = 2;

    /// <summary>调休上课日（按指定星期上课）。</summary>
    public const int SwapDay = 3;

    /// <summary>由「停课 + 补课」两条记录组成的调课（弹窗展示用，不落库为该值）。</summary>
    public const int SwapLesson = 4;

    /// <summary>类型显示文本。</summary>
    public static string GetText(int type)
    {
        return type switch
        {
            Cancelled => "停课",
            MakeUp => "补课",
            Holiday => "放假",
            SwapDay => "调休上课日",
            SwapLesson => "调课（停课+补课）",
            _ => $"未知类型({type})",
        };
    }

    /// <summary>是否为整天类型（不需要节次）。</summary>
    public static bool IsWholeDay(int type)
    {
        return type is Holiday or SwapDay;
    }

    /// <summary>星期显示文本。</summary>
    public static string GetWeekdayText(int weekday)
    {
        return weekday switch
        {
            1 => "周一",
            2 => "周二",
            3 => "周三",
            4 => "周四",
            5 => "周五",
            6 => "周六",
            7 => "周日",
            _ => $"星期{weekday}",
        };
    }
}
