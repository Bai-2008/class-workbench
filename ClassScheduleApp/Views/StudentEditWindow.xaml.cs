using System.Windows;
using ClassScheduleApp.ViewModels;

namespace ClassScheduleApp.Views;

/// <summary>
/// 学生新增/编辑窗口。只做装配与纯 UI 动作（设置 DataContext、按 ViewModel 请求关闭对话框）。
/// </summary>
public partial class StudentEditWindow : Window
{
    public StudentEditWindow(StudentEditViewModel viewModel)
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
