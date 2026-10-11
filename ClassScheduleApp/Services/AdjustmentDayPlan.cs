using ClassScheduleApp.Models;

namespace ClassScheduleApp.Services;

/// <summary>
/// 某一天的调整生效结果（供课表页逐列叠加与空闲统计换算使用）。
/// </summary>
public sealed class AdjustmentDayPlan
{
    public AdjustmentDayPlan(
        DateOnly date,
        bool isHoliday,
        int? swapWeekday,
        IReadOnlyList<ScheduleAdjustment> cancellations,
        IReadOnlyList<ScheduleAdjustment> makeUps)
    {
        Date = date;
        IsHoliday = isHoliday;
        SwapWeekday = swapWeekday;
        Cancellations = cancellations;
        MakeUps = makeUps;
    }

    /// <summary>该日日历日期。</summary>
    public DateOnly Date { get; }

    /// <summary>是否放假（整天）。</summary>
    public bool IsHoliday { get; }

    /// <summary>调休上课日：该日按周几的课表上课（null = 不是调休日）。</summary>
    public int? SwapWeekday { get; }

    /// <summary>是否调休上课日。</summary>
    public bool IsSwapDay => SwapWeekday.HasValue;

    /// <summary>该日的停课记录。</summary>
    public IReadOnlyList<ScheduleAdjustment> Cancellations { get; }

    /// <summary>该日的补课记录。</summary>
    public IReadOnlyList<ScheduleAdjustment> MakeUps { get; }

    /// <summary>列标记文本：放假 / 上周X课 / 空。</summary>
    public string MarkText => IsHoliday
        ? "放假"
        : SwapWeekday.HasValue ? $"上周{ScheduleAdjustmentTypes.GetWeekdayText(SwapWeekday.Value)[1..]}课" : string.Empty;

    /// <summary>是否有任何调整生效。</summary>
    public bool HasAdjustment => IsHoliday || IsSwapDay || Cancellations.Count > 0 || MakeUps.Count > 0;
}
