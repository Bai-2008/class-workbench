using ClassScheduleApp.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 空闲矩阵中的一格（页面用）：负责展示文字与选中/全班空闲高亮状态。
/// </summary>
public partial class ClassFreeCellViewModel : ObservableObject
{
    public ClassFreeCellViewModel(ClassFreeCell cell)
    {
        DayOfWeek = cell.DayOfWeek;
        SlotIndex = cell.SlotIndex;
        StartPeriod = cell.StartPeriod;
        EndPeriod = cell.EndPeriod;
        FreeCount = cell.FreeCount;
        TotalCount = cell.TotalCount;
        FreeStudentNames = cell.FreeStudentNames;
        WeekdayText = cell.WeekdayText;
        PeriodText = cell.PeriodText;
    }

    /// <summary>星期，1–7。</summary>
    public int DayOfWeek { get; }

    /// <summary>节次对下标，0–5。</summary>
    public int SlotIndex { get; }

    /// <summary>起始节次。</summary>
    public int StartPeriod { get; }

    /// <summary>结束节次。</summary>
    public int EndPeriod { get; }

    /// <summary>空闲人数。</summary>
    public int FreeCount { get; }

    /// <summary>全班人数。</summary>
    public int TotalCount { get; }

    /// <summary>空闲学生姓名（导出与详情一致）。</summary>
    public IReadOnlyList<string> FreeStudentNames { get; }

    /// <summary>星期文本。</summary>
    public string WeekdayText { get; }

    /// <summary>节次对文本，如 1-2节。</summary>
    public string PeriodText { get; }

    /// <summary>是否全班都空闲（高亮条件）。</summary>
    public bool IsAllFree => TotalCount > 0 && FreeCount == TotalCount;

    /// <summary>格子内文字。</summary>
    public string FreeText => $"{FreeCount}人空闲";

    /// <summary>是否为当前选中格。</summary>
    [ObservableProperty]
    private bool _isSelected;
}
