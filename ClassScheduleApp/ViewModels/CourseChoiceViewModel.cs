using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 课表冲突时的候选课程选择弹窗：选中一门后由调用方打开既有课程编辑弹窗。
/// </summary>
public partial class CourseChoiceViewModel : DialogViewModelBase
{
    public CourseChoiceViewModel(string title, IReadOnlyList<CourseChoiceItem> options)
    {
        Title = title;
        Options = options;
        SelectedOption = options.Count > 0 ? options[0] : null;
    }

    /// <summary>候选课程。</summary>
    public IReadOnlyList<CourseChoiceItem> Options { get; }

    /// <summary>当前选中的候选课程。</summary>
    [ObservableProperty]
    private CourseChoiceItem? _selectedOption;

    /// <inheritdoc />
    protected override bool CanConfirm()
    {
        return SelectedOption is not null;
    }
}
