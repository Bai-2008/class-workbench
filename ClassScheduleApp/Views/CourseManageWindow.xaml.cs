using System.ComponentModel;
using System.Windows;
using ClassScheduleApp.ViewModels;

namespace ClassScheduleApp.Views;

/// <summary>
/// 课程管理窗口。只做装配与纯 UI 动作；
/// 课程数据的加载由 DialogService 在显示对话框前完成（见 Views/DialogService.cs）。
/// </summary>
public partial class CourseManageWindow : Window
{
    private readonly CourseManageViewModel _viewModel;

    public CourseManageWindow(CourseManageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, bool hasChanges)
    {
        // 用户可能已用右上角关闭窗口，此时不能再写 DialogResult（会抛异常）
        if (IsLoaded && DialogResult is null)
        {
            DialogResult = hasChanges;
        }
    }

    /// <summary>用右上角关闭时，同样按“是否改过课程”回传结果，保证主界面课表能刷新。</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (IsLoaded && DialogResult is null)
        {
            DialogResult = _viewModel.HasChanges;
        }
    }
}
