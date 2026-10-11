namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 周视图网格的一个单元格：某天某起始节的课程（可能多门 = 课表冲突，格内紧凑纵排）。
/// 连堂课按 StartPeriod~EndPeriod 跨行合并（RowSpan）；调休叠加后同一格可能同时含停课/补课条目。
/// </summary>
public sealed class WeekGridCellItem : WeekGridItemViewModel
{
    public WeekGridCellItem(
        int dayOfWeek,
        int startPeriod,
        int endPeriod,
        IReadOnlyList<WeekGridCourseEntry> entries,
        int row,
        int column,
        bool isHoliday)
    {
        DayOfWeek = dayOfWeek;
        StartPeriod = startPeriod;
        EndPeriod = endPeriod;
        Entries = entries;
        Row = row;
        Column = column;
        RowSpan = endPeriod - startPeriod + 1;
        IsHoliday = isHoliday;
    }

    /// <summary>星期，1–7。</summary>
    public int DayOfWeek { get; }

    /// <summary>本格起始节次。</summary>
    public int StartPeriod { get; }

    /// <summary>本格结束节次（连堂课跨行用）。</summary>
    public int EndPeriod { get; }

    /// <summary>本格的课程条目（≥1；含停课/补课标记）。</summary>
    public IReadOnlyList<WeekGridCourseEntry> Entries { get; }

    /// <summary>该列是否放假。</summary>
    public bool IsHoliday { get; }

    /// <summary>是否有课（空白格为 false）。</summary>
    public bool HasCourse => Entries.Count > 0;

    /// <summary>是否课表冲突（同格多门课）。</summary>
    public bool HasConflict => Entries.Count > 1;

    /// <summary>冲突提示文本。</summary>
    public string ConflictText => HasConflict ? $"本格 {Entries.Count} 门课，点击选择" : string.Empty;

    /// <summary>仅一门课时直接引用的条目（供双击编辑）。</summary>
    public WeekGridCourseEntry? SingleEntry => Entries.Count == 1 ? Entries[0] : null;
}
