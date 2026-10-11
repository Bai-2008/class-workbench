using System.Windows;
using ClassScheduleApp.ViewModels;

namespace ClassScheduleApp.Views;

/// <summary>
/// 选课窗口。只做装配与纯 UI 动作；课程与已选状态的加载由 DialogService 在显示前完成。
/// </summary>
public partial class EnrollmentWindow : Window
{
    public EnrollmentWindow(EnrollmentViewModel viewModel)
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
