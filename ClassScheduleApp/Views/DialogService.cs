using System.ComponentModel;
using System.Windows;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using ClassScheduleApp.Services;
using ClassScheduleApp.ViewModels;
using ClassScheduleApp.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace ClassScheduleApp.Views;

/// <summary>
/// 对话框服务的表现层实现：负责创建窗口并装配对应 ViewModel。
/// ViewModel 只依赖 Services/IDialogService 接口，不引用 System.Windows；
/// 由于依赖方向是 Views → ViewModels → Services，实现放在 Views 层不会形成反向引用。
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _services;

    public DialogService(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc />
    public bool ShowStudentEditor(Student? student)
    {
        var viewModel = _services.GetRequiredService<StudentEditViewModel>();
        viewModel.Initialize(student);

        var window = new StudentEditWindow(viewModel) { Owner = GetOwner() };
        return window.ShowDialog() == true;
    }

    /// <summary>
    /// 取当前活动窗口作为 Owner：课程管理窗口是模态的，从它里面再打开编辑窗口时，
    /// 若 Owner 仍是主窗口会造成模态层级错误（子对话框可能被禁用或落到后面）。
    /// </summary>
    private static Window? GetOwner()
    {
        var application = Application.Current;
        if (application is null)
        {
            return null;
        }

        foreach (Window window in application.Windows)
        {
            if (window.IsActive)
            {
                return window;
            }
        }

        return application.MainWindow;
    }

    /// <summary>
    /// 打开对话框前的数据加载：await 不会阻塞 UI 线程（调用方是 async 命令），
    /// 加载完成后再 ShowDialog，避免窗口先显示空列表。
    /// </summary>
    public async Task<bool> ShowCourseManagerAsync()
    {
        var viewModel = _services.GetRequiredService<CourseManageViewModel>();
        await viewModel.InitializeAsync();

        var window = new CourseManageWindow(viewModel) { Owner = GetOwner() };
        return window.ShowDialog() == true;
    }

    /// <inheritdoc />
    public bool ShowCourseEditor(CourseDetail? course)
    {
        var viewModel = _services.GetRequiredService<CourseEditViewModel>();
        viewModel.Initialize(course);

        var window = new CourseEditWindow(viewModel) { Owner = GetOwner() };
        return window.ShowDialog() == true;
    }

    /// <inheritdoc />
    public async Task<bool> ShowEnrollmentAsync(Student student)
    {
        var viewModel = _services.GetRequiredService<EnrollmentViewModel>();
        await viewModel.InitializeAsync(student);

        var window = new EnrollmentWindow(viewModel) { Owner = GetOwner() };
        return window.ShowDialog() == true;
    }

    /// <inheritdoc />
    public bool Confirm(string message, string title)
    {
        var owner = GetOwner();
        var result = owner is null
            ? MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }

    /// <inheritdoc />
    public void ShowError(string message, string title)
    {
        var owner = GetOwner();
        if (owner is null)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <inheritdoc />
    public bool? ShowDialog<TVm>(TVm viewModel)
        where TVm : IDialogViewModel
    {
        var window = new DialogShell
        {
            Owner = GetOwner(),
            DataContext = viewModel,
        };

        // ViewModel 设置 DialogResult 即关闭弹窗（设为 null 表示还没决定，不关）
        void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(IDialogViewModel.DialogResult) && viewModel.DialogResult.HasValue)
            {
                window.DialogResult = viewModel.DialogResult;
            }
        }

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        try
        {
            return window.ShowDialog();
        }
        finally
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}
