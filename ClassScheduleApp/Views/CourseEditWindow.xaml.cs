using System.Windows;
using ClassScheduleApp.ViewModels;

namespace ClassScheduleApp.Views;

/// <summary>
/// 课程新增/编辑窗口。只做装配与纯 UI 动作。
/// </summary>
public partial class CourseEditWindow : Window
{
    public CourseEditWindow(CourseEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, bool dialogResult)
    {
        // 用户可能已用右上角关闭窗口，此时不能再写 DialogResult（会抛异常）
        if (IsLoaded && DialogResult is null)
        {
            DialogResult = dialogResult;
        }
    }
}
