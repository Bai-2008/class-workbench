namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 周视图网格里的分隔行（午休 / 晚休），为视图常量，不落库。
/// </summary>
public sealed class WeekGridSeparatorItem : WeekGridItemViewModel
{
    public WeekGridSeparatorItem(string text, int row)
    {
        Text = text;
        Row = row;
        Column = 0;
        ColumnSpan = 8;
    }

    /// <summary>分隔文本：午休 / 晚休。</summary>
    public string Text { get; }

    /// <summary>供下拉读取显示文本（WPF 的项名称取 ToString，避免显示类型全名）。</summary>
    public override string ToString() => Text;
}
