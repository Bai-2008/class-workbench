namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 周视图网格里可定位的一项：由 ViewModel 给出它在 Grid 中的行/列与跨行跨列。
/// 网格用一个 ItemsControl + Grid 面板渲染，容器位置由这里的数据决定。
/// </summary>
public abstract class WeekGridItemViewModel
{
    /// <summary>所在行（0 = 日期表头行）。</summary>
    public int Row { get; init; }

    /// <summary>所在列（0 = 节次标签列，1–7 = 周一至周日）。</summary>
    public int Column { get; init; }

    /// <summary>跨行数（连堂课合并）。</summary>
    public int RowSpan { get; init; } = 1;

    /// <summary>跨列数。</summary>
    public int ColumnSpan { get; init; } = 1;
}
