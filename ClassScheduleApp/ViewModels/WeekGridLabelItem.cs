namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 周视图网格的节次行标签：节次号 + 起止时间（复用作息时间表数据）。
/// </summary>
public sealed class WeekGridLabelItem : WeekGridItemViewModel
{
    public WeekGridLabelItem(int periodNo, string periodText, string timeText, int row)
    {
        PeriodNo = periodNo;
        PeriodText = periodText;
        TimeText = timeText;
        Row = row;
        Column = 0;
    }

    /// <summary>节次号。</summary>
    public int PeriodNo { get; }

    /// <summary>节次文本，如 "第 3 节"。</summary>
    public string PeriodText { get; }

    /// <summary>起止时刻文本，如 "10:25-11:10"；作息未载入时为空。</summary>
    public string TimeText { get; }
}
