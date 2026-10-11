using System.Collections.ObjectModel;
using ClassScheduleApp.Models;
using ClassScheduleApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 「调休补课」模块页：学期选择 + 调整列表 + 添加/编辑/删除。
/// 前置依赖：SemesterSettings 起始日必须已配置；未配置时本模块整体禁用并给出引导提示。
/// </summary>
public partial class ScheduleAdjustmentViewModel : ObservableObject
{
    private readonly IScheduleAdjustmentService _adjustmentService;
    private readonly ISemesterService _semesterService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigation;
    private readonly IBackupService _backupService;

    /// <summary>课程 Id → 课程名（列表展示用）。</summary>
    private readonly Dictionary<int, string> _courseNames = new();

    public ScheduleAdjustmentViewModel(
        IScheduleAdjustmentService adjustmentService,
        ISemesterService semesterService,
        IDialogService dialogService,
        INavigationService navigation,
        IBackupService backupService)
    {
        _adjustmentService = adjustmentService;
        _semesterService = semesterService;
        _dialogService = dialogService;
        _navigation = navigation;
        _backupService = backupService;

        // 备份恢复 → 数据库已被整体替换，调整列表重新查询
        _backupService.Restored += (_, _) => { _ = ReloadAsync(); };

        // 进入本模块时重新载入（起始日可能在设置页刚配置好）
        _navigation.CurrentModuleChanged += OnCurrentModuleChanged;

        // 首次进入时导航事件可能早于本构造函数（订阅赶不上），这里主动载入一次，
        // 否则会出现"第一次进来学期栏空白、切走再回来才正常"的现象
        _ = ReloadAsync();
    }

    /// <summary>页顶说明。</summary>
    public string ImpactHint => "调整即时影响课表页与空闲统计。调课由「停课 + 补课」两条记录组成（一次事务写入）；放假按天展开。";

    /// <summary>学期下拉。</summary>
    [ObservableProperty]
    private ObservableCollection<string> _semesters = new();

    /// <summary>当前学期。</summary>
    [ObservableProperty]
    private string? _selectedSemester;

    /// <summary>调整列表（按日期升序）。</summary>
    [ObservableProperty]
    private ObservableCollection<ScheduleAdjustmentRowViewModel> _adjustments = new();

    /// <summary>状态提示。</summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>模块是否可用（起始日已配置）。</summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>禁用原因（起始日未配置时的引导提示）。</summary>
    [ObservableProperty]
    private string? _disabledHint;

    private void OnCurrentModuleChanged(object? sender, EventArgs e)
    {
        if (_navigation.CurrentModule?.Id == FeatureRegistry.ScheduleAdjustmentId)
        {
            _ = ReloadAsync();
        }
    }

    /// <summary>重建学期列表期间抑制「选中变化」重入，避免自己触发自己。</summary>
    private bool _isReloading;

    partial void OnSelectedSemesterChanged(string? value)
    {
        if (_isReloading)
        {
            return;
        }

        _ = ReloadAsync();
    }

    /// <summary>载入学期、起始日可用性与调整列表。</summary>
    public async Task ReloadAsync()
    {
        try
        {
            await _semesterService.LoadAsync();
            var semesters = await _semesterService.GetSemestersAsync();

            var previous = SelectedSemester;
            var target = semesters.Count > 0 ? semesters[0] : null;
            foreach (var semester in semesters)
            {
                if (semester == previous)
                {
                    target = semester;
                    break;
                }
            }

            // 替换 ItemsSource 会让 ComboBox 丢掉选中显示，而"值没变"又不会发变更通知，
            // 结果就是下拉框永远空白（本页每次进入都会重载，所以只有它犯这个病）。
            // 做法：先清空、再换集合、最后赋回目标值 —— 保证一定发生一次真实变更。
            _isReloading = true;
            try
            {
                SelectedSemester = null;
                Semesters = new ObservableCollection<string>(semesters);
                SelectedSemester = target;
            }
            finally
            {
                _isReloading = false;
            }

            await LoadAdjustmentsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载调休补课数据失败：{ex.Message}";
        }
    }

    private async Task LoadAdjustmentsAsync()
    {
        var semester = SelectedSemester;

        // 起始日未配置 → 本模块整体禁用（不做任何叠加）
        var available = await _adjustmentService.LoadAsync(semester);
        IsEnabled = available;

        if (!available || string.IsNullOrWhiteSpace(semester))
        {
            Adjustments = new ObservableCollection<ScheduleAdjustmentRowViewModel>();
            _courseNames.Clear();
            DisabledHint = "该学期尚未配置学期起始日，本模块已禁用：请先到「设置 → 学期设置」把起始日设为第 1 周周一，再回到本页。";
            StatusMessage = null;
            return;
        }

        DisabledHint = null;

        _courseNames.Clear();
        var courses = await _adjustmentService.GetCoursesAsync(semester);
        foreach (var course in courses)
        {
            _courseNames[course.Id] = course.DisplayText;
        }

        var items = await _adjustmentService.GetBySemesterAsync(semester);
        var rows = new List<ScheduleAdjustmentRowViewModel>();
        foreach (var item in items)
        {
            rows.Add(new ScheduleAdjustmentRowViewModel(item, ResolveCourseName(item.CourseId)));
        }

        Adjustments = new ObservableCollection<ScheduleAdjustmentRowViewModel>(rows);
        StatusMessage = $"{semester} 共 {rows.Count} 条调整（叠加到课表页与空闲统计）。";
    }

    private string ResolveCourseName(int? courseId)
    {
        if (courseId is null)
        {
            return string.Empty;
        }

        return _courseNames.TryGetValue(courseId.Value, out var name) ? name : $"课程#{courseId.Value}";
    }

    /// <summary>添加调整。</summary>
    [RelayCommand]
    private async Task AddAsync()
    {
        var semester = SelectedSemester;
        if (!IsEnabled || string.IsNullOrWhiteSpace(semester))
        {
            _dialogService.ShowError(DisabledHint ?? "请先选择学期。", "调休补课");
            return;
        }

        var courses = await _adjustmentService.GetCoursesAsync(semester);
        var editor = new ScheduleAdjustmentEditViewModel(semester, courses);
        if (_dialogService.ShowDialog(editor) != true)
        {
            return;
        }

        var items = editor.Build();
        var result = await _adjustmentService.SaveRangeAsync(items);
        if (!result.Success)
        {
            _dialogService.ShowError(result.ErrorMessage ?? "保存调整失败。", "调休补课");
            return;
        }

        await LoadAdjustmentsAsync();
        StatusMessage = $"已保存 {items.Count} 条调整记录" + (items.Count > 1 ? "（一次事务写入）。" : "。");
    }

    /// <summary>编辑一条调整。</summary>
    [RelayCommand]
    private async Task EditAsync(ScheduleAdjustmentRowViewModel? row)
    {
        var semester = SelectedSemester;
        if (row is null || !IsEnabled || string.IsNullOrWhiteSpace(semester))
        {
            return;
        }

        var courses = await _adjustmentService.GetCoursesAsync(semester);
        var editor = new ScheduleAdjustmentEditViewModel(semester, courses, row.Adjustment);
        if (_dialogService.ShowDialog(editor) != true)
        {
            return;
        }

        var items = editor.Build();
        if (items.Count == 1)
        {
            var result = await _adjustmentService.UpdateAsync(items[0]);
            if (!result.Success)
            {
                _dialogService.ShowError(result.ErrorMessage ?? "更新调整失败。", "调休补课");
                return;
            }

            await LoadAdjustmentsAsync();
            StatusMessage = "已更新该条调整。";
            return;
        }

        // 改成调课/放假（多条）：删原记录 + 事务写入新记录，语义等价于"替换"
        var deleteResult = await _adjustmentService.DeleteAsync(row.Adjustment.Id);
        if (!deleteResult.Success)
        {
            _dialogService.ShowError(deleteResult.ErrorMessage ?? "替换调整失败。", "调休补课");
            return;
        }

        var saveResult = await _adjustmentService.SaveRangeAsync(items);
        if (!saveResult.Success)
        {
            _dialogService.ShowError(saveResult.ErrorMessage ?? "保存调整失败。", "调休补课");
            return;
        }

        await LoadAdjustmentsAsync();
        StatusMessage = $"已替换为 {items.Count} 条调整记录。";
    }

    /// <summary>删除一条调整（Confirm 确认）。</summary>
    /// <summary>
    /// 批量删除所选调整记录：确认框列出条数，执行前自动备份，逐条删除（每条广播变更，
    /// 课表页与该周空闲统计据此即时反映：补课格消失、调休列恢复原星期）。
    /// </summary>
    [RelayCommand]
    private async Task DeleteSelectedAsync(IReadOnlyList<ScheduleAdjustmentRowViewModel>? rows)
    {
        var targets = rows is null
            ? new List<ScheduleAdjustmentRowViewModel>()
            : new List<ScheduleAdjustmentRowViewModel>(rows);

        if (targets.Count == 0)
        {
            StatusMessage = "请先在列表里选中要删除的调整记录（可按住 Ctrl 或 Shift 多选）。";
            return;
        }

        var confirm = new ConfirmActionViewModel
        {
            Title = "批量删除调整记录",
            Subject = $"共选中 {targets.Count} 条调整记录",
            Notice = "删除后课表页与该周空闲统计会即时恢复为未调整状态。",
        };
        confirm.ImpactLines.Add($"所选的 {targets.Count} 条调整记录（停课 / 补课 / 放假 / 调休上课日）");
        confirm.RetainedLines.Add("学生、课程与考勤记录（不受影响）");

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
            backupNote = "备份失败（不影响本次删除）：" + ex.Message;
        }

        var deleted = 0;
        var failures = new List<string>();
        foreach (var row in targets)
        {
            var result = await _adjustmentService.DeleteAsync(row.Adjustment.Id);
            if (result.Success)
            {
                deleted++;
            }
            else
            {
                failures.Add(result.ErrorMessage ?? $"调整 #{row.Adjustment.Id} 删除失败");
            }
        }

        await ReloadAsync();
        StatusMessage = $"已删除 {deleted} 条调整记录" +
            (failures.Count == 0 ? "。" : $"；失败 {failures.Count} 条：{string.Join("；", failures)}") +
            "。" + backupNote;
    }

    [RelayCommand]
    private async Task DeleteAsync(ScheduleAdjustmentRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var confirmed = _dialogService.Confirm(
            $"确定删除这条调整吗？\r\n\r\n{row.DateText}  {row.TypeText}  {row.CourseText}  {row.PeriodText}",
            "删除调整");
        if (!confirmed)
        {
            return;
        }

        var result = await _adjustmentService.DeleteAsync(row.Adjustment.Id);
        if (!result.Success)
        {
            _dialogService.ShowError(result.ErrorMessage ?? "删除调整失败。", "调休补课");
            return;
        }

        await LoadAdjustmentsAsync();
        StatusMessage = "已删除该条调整。";
    }
}
