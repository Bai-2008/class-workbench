using ClassScheduleApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 课程编辑弹窗里的一行上课时段：星期 / 起始节 / 结束节 / 周次起止 / 教室。
/// 每个下拉的选项由行自己提供，避免在 DataTemplate 里跨层取 DataContext。
/// </summary>
public partial class CourseScheduleRowViewModel : ObservableObject
{
    private static readonly int[] PeriodValues = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
    private static readonly int[] WeekValues = Enumerable.Range(1, 25).ToArray();

    /// <summary>排课主键；0 表示尚未落库的新行。</summary>
    public int Id { get; init; }

    /// <summary>节次可选项（1–12 节）。</summary>
    public IReadOnlyList<int> PeriodOptions { get; } = PeriodValues;

    /// <summary>周次可选项（1–25 周）。</summary>
    public IReadOnlyList<int> WeekOptions { get; } = WeekValues;

    /// <summary>星期下拉的选中下标（0=周一 … 6=周日）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowTitle))]
    private int _dayIndex;

    /// <summary>起始节次。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowTitle))]
    private int _startPeriod = 1;

    /// <summary>结束节次。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowTitle))]
    private int _endPeriod = 2;

    /// <summary>起始周。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowTitle))]
    private int _startWeek = 1;

    /// <summary>结束周。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowTitle))]
    private int _endWeek = 16;

    /// <summary>上课教室（选填）。</summary>
    [ObservableProperty]
    private string _location = string.Empty;

    /// <summary>单双周可选项（每周 / 单周 / 双周）。</summary>
    public IReadOnlyList<string> ParityOptions { get; } = WeekParityTypes.AllDisplay;

    /// <summary>单双周下拉的选中文本，默认「每周」（既有课程保存后仍为每周）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowTitle))]
    private string _selectedParity = WeekParityTypes.ToDisplay(WeekParityTypes.Every);

    /// <summary>行标题，便于核对本行录入内容。</summary>
    public string RowTitle
    {
        get
        {
            var parity = WeekParityTypes.FromDisplay(SelectedParity);
            var parityText = parity == WeekParityTypes.Every ? string.Empty : $"（{SelectedParity}）";
            return $"{WeekdayText} {StartPeriod}-{EndPeriod}节 {StartWeek}-{EndWeek}周{parityText}";
        }
    }

    /// <summary>星期展示文本。</summary>
    public string WeekdayText => DayIndex switch
    {
        0 => "周一",
        1 => "周二",
        2 => "周三",
        3 => "周四",
        4 => "周五",
        5 => "周六",
        6 => "周日",
        _ => "周一",
    };
}
