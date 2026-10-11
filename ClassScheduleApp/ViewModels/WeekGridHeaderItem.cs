namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 周视图网格的日期表头格（周一 10/6，今天高亮）。
/// </summary>
public sealed class WeekGridHeaderItem : WeekGridItemViewModel
{
    public WeekGridHeaderItem(DayHeaderItem header, int column, string markText = "")
    {
        Header = header;
        Column = column;
        MarkText = markText;
    }

    /// <summary>复用列表视图的日期表头数据（同一数据源两处渲染）。</summary>
    public DayHeaderItem Header { get; }

    /// <summary>调整标记：放假 / 上周X课 / 空。</summary>
    public string MarkText { get; }

    /// <summary>是否有调整标记。</summary>
    public bool HasMark => MarkText.Length > 0;
}
