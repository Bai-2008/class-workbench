using ClassScheduleApp.Models;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 调整列表里的一行：日期 / 类型 / 课程 / 节次 / 备注（展示用，不改动底层实体）。
/// </summary>
public sealed class ScheduleAdjustmentRowViewModel
{
    public ScheduleAdjustmentRowViewModel(ScheduleAdjustment adjustment, string courseName)
    {
        Adjustment = adjustment;
        DateText = adjustment.AffectedDate.ToString("yyyy-MM-dd");
        TypeText = ScheduleAdjustmentTypes.GetText(adjustment.AdjustmentType);
        CourseText = courseName.Length > 0 ? courseName : (ScheduleAdjustmentTypes.IsWholeDay(adjustment.AdjustmentType) ? "（整天）" : "（未指定课程）");

        if (adjustment.StartPeriod.HasValue && adjustment.EndPeriod.HasValue)
        {
            PeriodText = adjustment.StartPeriod == adjustment.EndPeriod
                ? $"第 {adjustment.StartPeriod} 节"
                : $"第 {adjustment.StartPeriod}-{adjustment.EndPeriod} 节";
        }
        else
        {
            PeriodText = "—";
        }

        if (adjustment.AdjustmentType == ScheduleAdjustmentTypes.SwapDay && adjustment.FollowWeekday.HasValue)
        {
            PeriodText = $"按{ScheduleAdjustmentTypes.GetWeekdayText(adjustment.FollowWeekday.Value)}课表";
        }

        NoteText = adjustment.Note ?? string.Empty;
        LocationText = adjustment.NewLocation ?? string.Empty;
    }

    /// <summary>底层记录（编辑与删除用）。</summary>
    public ScheduleAdjustment Adjustment { get; }

    /// <summary>日期文本。</summary>
    public string DateText { get; }

    /// <summary>类型文本。</summary>
    public string TypeText { get; }

    /// <summary>课程文本。</summary>
    public string CourseText { get; }

    /// <summary>节次文本。</summary>
    public string PeriodText { get; }

    /// <summary>备注文本。</summary>
    public string NoteText { get; }

    /// <summary>补课教室文本。</summary>
    public string LocationText { get; }
}
