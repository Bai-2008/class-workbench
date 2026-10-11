namespace ClassScheduleApp.Models;

/// <summary>
/// 作息时间实体，对应 PeriodTimes 表（一表一类）：每日第几节的起止时刻，精确到分钟。
/// 由设置页「作息时间」区块整体维护；课表页与空闲统计页只读它做行头显示。
/// </summary>
public class PeriodTime
{
    /// <summary>主键。</summary>
    public int Id { get; set; }

    /// <summary>节次号（1–12），唯一。</summary>
    public int PeriodNo { get; set; }

    /// <summary>本节开始时刻。</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>本节结束时刻。</summary>
    public TimeOnly EndTime { get; set; }
}
