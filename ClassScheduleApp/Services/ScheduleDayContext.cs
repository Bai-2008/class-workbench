namespace ClassScheduleApp.Services;

/// <summary>
/// 「某日期」的学期上下文：日期 → 学期 → 教学周的统一解析结果。
/// 供考勤登记「日期 → 课程列表 → 名单」一条链路共用，避免各处各算一遍。
/// </summary>
public sealed class ScheduleDayContext
{
    /// <summary>该日期所属学期；不属于任何已配置学期时为 null。</summary>
    public string? Semester { get; set; }

    /// <summary>教学周序号；起始日未配置时为 null（降级：仅按星期过滤）。</summary>
    public int? Week { get; set; }

    /// <summary>
    /// 该日期在教学周内的列号 1–7（应用统一约定：1 = 起始日当天，逐日 +1）。
    /// 注意**不是真实星期几**——库内课程时段的 DayOfWeek 同样按此列号存储，两处必须一致。
    /// </summary>
    public int Weekday { get; set; } = 1;

    /// <summary>该学期配置的总周数。</summary>
    public int WeekCount { get; set; }

    /// <summary>是否已配置起始日。</summary>
    public bool IsConfigured { get; set; }

    /// <summary>该日期是否超出教学周范围（此时课程列表应为空）。</summary>
    public bool IsOutsideTeachingWeeks { get; set; }

    /// <summary>降级/边界提示文本（无问题时为空）。</summary>
    public string Hint { get; set; } = string.Empty;
}
