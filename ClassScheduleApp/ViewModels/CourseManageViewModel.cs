using System.Collections.ObjectModel;
using ClassScheduleApp.Repositories;
using ClassScheduleApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 课程管理窗口的 ViewModel：课程列表 + 新增/编辑。
/// </summary>
public partial class CourseManageViewModel : ObservableObject
{
    private readonly ICourseAdminRepository _repository;
    private readonly IDialogService _dialogService;
    private readonly IBackupService _backupService;

    /// <summary>本次会话是否发生过新增/编辑，用于关闭窗口时通知主界面刷新。</summary>
    private bool _hasChanges;

    public CourseManageViewModel(
        ICourseAdminRepository repository,
        IDialogService dialogService,
        IBackupService backupService)
    {
        _repository = repository;
        _dialogService = dialogService;
        _backupService = backupService;
    }

    /// <summary>请求关闭窗口，参数表示课程数据是否有变化。</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>本次会话是否改过课程（供窗口用右上角关闭时回传结果）。</summary>
    public bool HasChanges => _hasChanges;

    /// <summary>操作结果提示（删除结果 / 清空结果 / 备份路径）。</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>课程列表。</summary>
    [ObservableProperty]
    private ObservableCollection<CourseDetail> _courses = new();

    /// <summary>当前选中的课程。</summary>
    [ObservableProperty]
    private CourseDetail? _selectedCourse;

    /// <summary>打开窗口时加载课程列表。</summary>
    public async Task InitializeAsync()
    {
        await ReloadAsync();
    }

    [RelayCommand]
    private void AddCourse()
    {
        if (_dialogService.ShowCourseEditor(null))
        {
            _hasChanges = true;
            _ = ReloadAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedCourse))]
    private void EditCourse()
    {
        var course = SelectedCourse;
        if (course is null)
        {
            return;
        }

        if (_dialogService.ShowCourseEditor(course))
        {
            _hasChanges = true;
            _ = ReloadAsync();
        }
    }

    /// <summary>
    /// 删除指定课程（列表行内按钮直接传入该行）：确认框逐项列出影响面，
    /// 确认后删除课程 + 时段 + 关联 + 该课程的调整记录；考勤记录保留。
    /// </summary>
    [RelayCommand]
    private async Task DeleteCourseAsync(CourseDetail? course)
    {
        if (course is null)
        {
            return;
        }

        var impact = await _repository.GetDeleteImpactAsync(course.Id);

        var confirm = new ConfirmActionViewModel
        {
            Title = "删除课程",
            Subject = $"{course.CourseName}（{course.Teacher}）",
        };
        confirm.ImpactLines.Add("该课程及其全部时段");
        confirm.ImpactLines.Add($"关联学生 {impact.StudentCount} 人（一并解除关联）");
        confirm.ImpactLines.Add($"涉及该课程的调休补课调整 {impact.AdjustmentCount} 条（一并删除）");
        confirm.RetainedLines.Add($"考勤记录 {impact.AttendanceCount} 条（保留，历史仍显示课程名）");

        if (_dialogService.ShowDialog(confirm) != true)
        {
            return;
        }

        var result = await _repository.DeleteCourseAsync(course.Id);
        StatusMessage = result.Success
            ? $"已删除课程「{course.CourseName}（{course.Teacher}）」：解除关联 {impact.StudentCount} 条、删除调整 {impact.AdjustmentCount} 条；考勤记录 {impact.AttendanceCount} 条保留。"
            : result.ErrorMessage ?? "删除课程失败。";

        if (result.Success)
        {
            _hasChanges = true;
            await ReloadAsync();
        }
    }

    /// <summary>
    /// 清空所有人的课表（学期清空）：确认框列出将删与保留，需键入「清空」；
    /// 执行前自动备份，完成后提示备份路径（备份失败不阻塞，如实报出详情）。
    /// </summary>
    [RelayCommand]
    private async Task ClearAllSchedulesAsync()
    {
        var impact = await _repository.GetClearAllImpactAsync();

        var confirm = new ConfirmActionViewModel
        {
            Title = "清空所有人的课表",
            Subject = "本操作用于学期清空，完成后需重新导入或录入课表。",
            RequireTypedWord = true,
            ConfirmWord = "清空",
            Notice = "本操作用于学期清空，完成后需要重新导入或录入课表。",
        };
        confirm.ImpactLines.Add($"全部课程关联 {impact.EnrollmentCount} 条");
        confirm.ImpactLines.Add($"课程 {impact.CourseCount} 门（含时段 {impact.ScheduleSlotCount} 条）");
        confirm.ImpactLines.Add($"调休补课调整 {impact.AdjustmentCount} 条");
        confirm.RetainedLines.Add($"学生 {impact.StudentCount} 人");
        confirm.RetainedLines.Add($"考勤记录 {impact.AttendanceCount} 条（含课程名快照，不受影响）");

        if (_dialogService.ShowDialog(confirm) != true)
        {
            return;
        }

        string backupNote;
        try
        {
            backupNote = "备份：" + _backupService.Backup(BackupReasons.PreOp);
        }
        catch (Exception ex)
        {
            backupNote = $"备份失败（不影响本次清空）：{ex.Message}";
        }

        var result = await _repository.ClearAllSchedulesAsync();
        StatusMessage = result.Success
            ? $"已清空全部课表：关联 {impact.EnrollmentCount} 条、课程 {impact.CourseCount} 门、调整 {impact.AdjustmentCount} 条；学生 {impact.StudentCount} 人与考勤记录 {impact.AttendanceCount} 条保留。{backupNote}"
            : result.ErrorMessage ?? "清空全部课表失败。";

        if (result.Success)
        {
            _hasChanges = true;
            await ReloadAsync();
        }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, _hasChanges);
    }

    partial void OnSelectedCourseChanged(CourseDetail? value)
    {
        EditCourseCommand.NotifyCanExecuteChanged();
    }

    private bool HasSelectedCourse()
    {
        return SelectedCourse is not null;
    }

    private async Task ReloadAsync()
    {
        var courses = await _repository.GetAllCoursesAsync();
        var selectedId = SelectedCourse?.Id ?? 0;

        Courses = new ObservableCollection<CourseDetail>(courses);

        // 不用 LINQ：查询逻辑只允许出现在 Repositories 层
        CourseDetail? reselect = null;
        foreach (var course in Courses)
        {
            if (course.Id == selectedId)
            {
                reselect = course;
                break;
            }
        }

        SelectedCourse = reselect;
    }
}
