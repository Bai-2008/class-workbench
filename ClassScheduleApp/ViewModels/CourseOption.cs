using ClassScheduleApp.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 选课窗口里的一门课程（带勾选状态）。
/// 勾选状态由 <see cref="ToggleSelectionCommand"/> 驱动，界面 CheckBox 只做单向显示
/// （勾选类控件点击走 SetCurrentValue，不回写绑定源）。
/// </summary>
public partial class CourseOption : ObservableObject
{
    public CourseOption(CourseDetail course, bool isSelected)
    {
        CourseId = course.Id;
        DisplayText = $"{course.CourseNo} {course.CourseName}    {course.Teacher}    {course.WeekdayText} {course.PeriodText} {course.Location}";
        IsSelected = isSelected;
    }

    /// <summary>课程 Id（保存时使用）。</summary>
    public int CourseId { get; }

    /// <summary>列表里显示的完整描述。</summary>
    public string DisplayText { get; }

    /// <summary>是否勾选；已选课程在打开窗口时预勾选。</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>切换勾选状态（供界面 CheckBox 调用）。</summary>
    [RelayCommand]
    private void ToggleSelection()
    {
        IsSelected = !IsSelected;
    }
}
