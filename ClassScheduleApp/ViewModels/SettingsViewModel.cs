using System.Collections.ObjectModel;
using ClassScheduleApp.Models;
using ClassScheduleApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 设置页 ViewModel：列出全部可开关模块，开关即时生效，可一键恢复默认；
/// 并提供「数据备份」区块（立即备份数据，成功后显示备份路径，失败弹错误详情）。
/// 不引用其它 ViewModel，导航刷新通过 INavigationService 广播。
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly INavigationService _navigation;
    private readonly IBackupService _backupService;
    private readonly IDialogService _dialogService;
    private readonly ISemesterService _semesterService;
    private readonly IPeriodTimeService _periodTimeService;
    private readonly IThemeService _themeService;

    /// <summary>重建列表期间忽略开关事件，避免把读取动作当成用户修改写回磁盘。</summary>
    private bool _isReloading;

    public SettingsViewModel(
        ISettingsService settingsService,
        INavigationService navigation,
        IBackupService backupService,
        IDialogService dialogService,
        ISemesterService semesterService,
        IPeriodTimeService periodTimeService,
        IThemeService themeService)
    {
        _settingsService = settingsService;
        _navigation = navigation;
        _backupService = backupService;
        _dialogService = dialogService;
        _semesterService = semesterService;
        _periodTimeService = periodTimeService;
        _themeService = themeService;

        // 备份列表只读文件系统，构造时即可填充
        ReloadBackups();

        LoadModules();

        // 外观：读取已保存的主题（深色 / 浅色 / 跟随系统）
        _selectedTheme = AppThemeModes.ToDisplay(_settingsService.GetThemeMode());

        // 学期设置与作息时间按需加载：首次进入设置页时再读库（启动时库可能还没建好/还没播种）
        _navigation.CurrentModuleChanged += OnCurrentModuleChanged;
        if (_navigation.CurrentModule?.Id == FeatureRegistry.SettingsId)
        {
            _ = LoadSemesterAsync();
            _ = LoadPeriodTimesAsync();
        }
    }

    private void OnCurrentModuleChanged(object? sender, EventArgs e)
    {
        if (_navigation.CurrentModule?.Id == FeatureRegistry.SettingsId)
        {
            _ = LoadSemesterAsync();
            _ = LoadPeriodTimesAsync();
        }
    }

    /// <summary>可开关的模块（设置自身除外）。</summary>
    [ObservableProperty]
    private ObservableCollection<FeatureToggleItem> _modules = new();

    /// <summary>恢复默认：清空 settings.json 覆盖记录，导航栏回到默认可见性。</summary>
    [RelayCommand]
    private void RestoreDefaults()
    {
        _settingsService.ResetToDefaults();
        LoadModules();
        _navigation.NotifyVisibleModulesChanged();
    }

    // ── 外观区块（追加）──────────────────────────────────────────

    /// <summary>主题选项（跟随系统 / 浅色 / 深色）。</summary>
    public IReadOnlyList<string> ThemeOptions => AppThemeModes.All;

    /// <summary>当前选中的主题（展示文本）。</summary>
    [ObservableProperty]
    private string _selectedTheme = "跟随系统";

    /// <summary>外观区块的提示（例如"跟随系统"当前解析成什么）。</summary>
    [ObservableProperty]
    private string? _themeHint;

    partial void OnSelectedThemeChanged(string value)
    {
        var mode = AppThemeModes.ToMode(value);
        _themeService.Apply(mode);
        _settingsService.SetThemeMode(mode);

        var effective = AppThemeModes.ToDisplay(_themeService.IsDarkEffective ? AppThemeModes.Dark : AppThemeModes.Light);
        ThemeHint = mode == AppThemeModes.System
            ? $"已保存：跟随系统（系统当前为{effective}）。改系统主题后重新进入本页即可跟随。"
            : $"已保存：{value}。";
    }

    // ── 学期设置区块（追加）──────────────────────────────────────

    /// <summary>学期下拉（StudentCourses.Semester 去重，最新在前）。</summary>
    [ObservableProperty]
    private ObservableCollection<string> _semesterOptions = new();

    /// <summary>当前选中的学期。</summary>
    [ObservableProperty]
    private string? _selectedSemester;

    /// <summary>第 1 周周一。</summary>
    [ObservableProperty]
    private DateTime? _semesterStartDate;

    /// <summary>总周数（文本，保存时统一解析校验）。</summary>
    [ObservableProperty]
    private string _weekCountText = SemesterSettings.DefaultWeekCount.ToString();

    /// <summary>学期设置提示：第 1 周日期区间或未配置说明。</summary>
    [ObservableProperty]
    private string? _semesterHint;

    /// <summary>学期设置操作结果提示。</summary>
    [ObservableProperty]
    private string? _semesterStatusMessage;

    /// <summary>保存学期设置。</summary>
    [RelayCommand]
    private async Task SaveSemesterAsync()
    {
        var semester = SelectedSemester;
        if (string.IsNullOrWhiteSpace(semester))
        {
            SemesterStatusMessage = "请先选择学期。";
            return;
        }

        if (!int.TryParse(WeekCountText.Trim(), out var weekCount) ||
            weekCount < _semesterService.MinWeekCount ||
            weekCount > _semesterService.MaxWeekCount)
        {
            SemesterStatusMessage = $"总周数需在 {_semesterService.MinWeekCount}–{_semesterService.MaxWeekCount} 之间。";
            return;
        }

        DateOnly? startDate = SemesterStartDate.HasValue
            ? DateOnly.FromDateTime(SemesterStartDate.Value)
            : null;

        var result = await _semesterService.SaveAsync(semester, startDate, weekCount);
        if (!result.Success)
        {
            SemesterStatusMessage = result.ErrorMessage ?? "保存学期设置失败。";
            return;
        }

        UpdateSemesterHint();
        SemesterStatusMessage = startDate.HasValue
            ? $"已保存：{semester} 起始日 {startDate.Value:yyyy-MM-dd}、共 {weekCount} 周。"
            : $"已保存：{semester} 共 {weekCount} 周（未配置起始日，课表页按第 1 周显示并提示）。";
    }

    partial void OnSelectedSemesterChanged(string? value)
    {
        _pendingDeleteSemester = null;
        ApplySemesterSetting(value);
    }

    /// <summary>新增学期用的学期名输入（如 2027-2028-1）。</summary>
    [ObservableProperty]
    private string _newSemesterName = string.Empty;

    /// <summary>已提示过影响范围、等待二次确认删除的学期。</summary>
    private string? _pendingDeleteSemester;

    /// <summary>新增学期：仅创建学期设置（起始日与周数随后由「保存学期设置」写入）。</summary>
    [RelayCommand]
    private async Task AddSemesterAsync()
    {
        var name = NewSemesterName.Trim();
        if (name.Length == 0)
        {
            SemesterStatusMessage = "请先填写学期名，例如 2027-2028-1。";
            return;
        }

        var result = await _semesterService.SaveAsync(name, null, SemesterSettings.DefaultWeekCount);
        if (!result.Success)
        {
            SemesterStatusMessage = result.ErrorMessage ?? "新增学期失败。";
            return;
        }

        NewSemesterName = string.Empty;
        _pendingDeleteSemester = null;
        await LoadSemesterAsync();
        SelectedSemester = name;
        SemesterStatusMessage = $"已新增学期「{name}」：请选好起始日与总周数后点「保存学期设置」。";
    }

    /// <summary>删除当前学期：首次点击只提示影响范围，再点一次确认删除（先自动备份）。</summary>
    [RelayCommand]
    private async Task DeleteSemesterAsync()
    {
        var semester = SelectedSemester;
        if (string.IsNullOrEmpty(semester))
        {
            SemesterStatusMessage = "请先选择要删除的学期。";
            return;
        }

        var usage = await _semesterService.GetUsageAsync(semester);
        var firstClick = _pendingDeleteSemester != semester;

        if (usage.HasData && firstClick)
        {
            _pendingDeleteSemester = semester;
            SemesterStatusMessage =
                $"学期「{semester}」下还有 {usage.EnrollmentCount} 条学生选课、{usage.AdjustmentCount} 条调休补课记录。" +
                "再点一次「删除本学期」将连同这些数据一起删除。";
            return;
        }

        if (usage.HasData)
        {
            try
            {
                var backupPath = _backupService.Backup(BackupReasons.PreOp);
                SemesterStatusMessage = $"删除前已备份：{backupPath}";
            }
            catch (Exception ex)
            {
                SemesterStatusMessage = $"备份失败，已中止删除以免数据丢失：{ex.Message}";
                return;
            }
        }

        var result = await _semesterService.DeleteAsync(semester, includeScheduleData: true);
        if (!result.Success)
        {
            SemesterStatusMessage = result.ErrorMessage ?? "删除学期失败。";
            return;
        }

        _pendingDeleteSemester = null;
        await LoadSemesterAsync();
        SemesterStatusMessage = $"已删除学期「{semester}」" +
            (usage.HasData ? $"（同时删除 {usage.EnrollmentCount} 条选课、{usage.AdjustmentCount} 条调整）。" : "。");
    }

    private async Task LoadSemesterAsync()
    {
        try
        {
            await _semesterService.LoadAsync();
            var semesters = await _semesterService.GetSemestersAsync();

            SemesterOptions = new ObservableCollection<string>(semesters);

            if (SemesterOptions.Count == 0)
            {
                SemesterStatusMessage = "还没有可用学期：学期取自选课记录的学期，请先在学生选课里建立学期。";
                SemesterHint = null;
                return;
            }

            var previous = SelectedSemester;
            var target = SemesterOptions[0];
            foreach (var semester in SemesterOptions)
            {
                if (semester == previous)
                {
                    target = semester;
                    break;
                }
            }

            if (SelectedSemester != target)
            {
                SelectedSemester = target;
            }
            else
            {
                // 列表刚重建，值没变时不会有变更通知，需显式通知一次，避免下拉框显示空白
                OnPropertyChanged(nameof(SelectedSemester));
            }

            ApplySemesterSetting(target);
        }
        catch (Exception ex)
        {
            SemesterStatusMessage = $"加载学期设置失败：{ex.Message}";
        }
    }

    private void ApplySemesterSetting(string? semester)
    {
        if (string.IsNullOrWhiteSpace(semester))
        {
            return;
        }

        // 缓存已在 LoadSemesterAsync 里预热，这里用同步取值器，避免 UI 线程上做同步等待
        SemesterStartDate = _semesterService.GetStartDate(semester)?.ToDateTime(TimeOnly.MinValue);
        WeekCountText = _semesterService.GetWeekCount(semester).ToString();
        UpdateSemesterHint();
        SemesterStatusMessage = null;
    }

    private void UpdateSemesterHint()
    {
        var semester = SelectedSemester;
        if (string.IsNullOrWhiteSpace(semester))
        {
            SemesterHint = null;
            return;
        }

        var start = _semesterService.GetStartDate(semester);
        if (start is null)
        {
            SemesterHint = "未配置起始日：课表页将按第 1 周显示并提示配置。";
            return;
        }

        var weekCount = _semesterService.GetWeekCount(semester);
        SemesterHint = $"第 1 周：{start.Value:yyyy-MM-dd} 起；第 {weekCount} 周止于 {start.Value.AddDays(weekCount * 7 - 1):yyyy-MM-dd}。";
    }

    // ── 作息时间区块（追加）──────────────────────────────────────

    /// <summary>12 节作息时间行。</summary>
    [ObservableProperty]
    private ObservableCollection<PeriodTimeRowViewModel> _periodRows = new();

    /// <summary>作息时间保存结果提示。</summary>
    [ObservableProperty]
    private string? _periodStatusMessage;

    /// <summary>每天节数（文本，应用时解析校验）。</summary>
    [ObservableProperty]
    private string _periodCountText = "12";

    /// <summary>节数设置结果提示。</summary>
    [ObservableProperty]
    private string? _periodCountHint;

    /// <summary>按输入的节数增删作息行并保存（保留已有时刻，新增节次用默认时刻）。</summary>
    [RelayCommand]
    private async Task ApplyPeriodCountAsync()
    {
        if (!int.TryParse(PeriodCountText.Trim(), out var count))
        {
            PeriodCountHint = "节数需要填数字。";
            return;
        }

        var result = await _periodTimeService.SetPeriodCountAsync(count);
        if (!result.Success)
        {
            PeriodCountHint = result.ErrorMessage ?? "应用节数失败。";
            return;
        }

        await LoadPeriodTimesAsync();
        PeriodStatusMessage = $"已按每天 {_periodTimeService.PeriodCount} 节保存作息时间。";
        PeriodCountHint = $"当前 {_periodTimeService.PeriodCount} 节（可设 {_periodTimeService.MinPeriodCount}–{_periodTimeService.MaxPeriodCount} 节）。";
    }

    /// <summary>整体保存 12 节作息时间；保存后课表页与空闲统计行头即时刷新。</summary>
    [RelayCommand]
    private async Task SavePeriodTimesAsync()
    {
        if (PeriodRows.Count == 0)
        {
            PeriodStatusMessage = "作息时间还没加载出来，请重新进入设置页。";
            return;
        }

        var items = new List<PeriodTime>();
        var hasError = false;
        foreach (var row in PeriodRows)
        {
            if (!row.TryBuild(out var start, out var end))
            {
                hasError = true;
                continue;
            }

            items.Add(new PeriodTime { PeriodNo = row.PeriodNo, StartTime = start, EndTime = end });
        }

        if (hasError)
        {
            PeriodStatusMessage = "有行填写不正确，请按提示修正后再保存。";
            return;
        }

        var result = await _periodTimeService.SaveAsync(items);
        PeriodStatusMessage = result.Success
            ? $"已保存 {items.Count} 节作息时间；课表页与空闲统计页的行头已即时刷新。"
            : result.ErrorMessage ?? "保存作息时间失败。";
    }

    private async Task LoadPeriodTimesAsync()
    {
        try
        {
            await _periodTimeService.LoadAsync();
            var all = _periodTimeService.GetAll();

            var rows = new List<PeriodTimeRowViewModel>();
            var count = _periodTimeService.PeriodCount;
            PeriodCountText = count.ToString();
            PeriodCountHint = $"当前 {count} 节（可设 {_periodTimeService.MinPeriodCount}–{_periodTimeService.MaxPeriodCount} 节）。";

            for (var period = 1; period <= count; period++)
            {
                var start = new TimeOnly(8, 0);
                var end = new TimeOnly(8, 45);
                foreach (var item in all)
                {
                    if (item.PeriodNo == period)
                    {
                        start = item.StartTime;
                        end = item.EndTime;
                        break;
                    }
                }

                rows.Add(new PeriodTimeRowViewModel(period, start, end));
            }

            PeriodRows = new ObservableCollection<PeriodTimeRowViewModel>(rows);
            PeriodStatusMessage = null;
        }
        catch (Exception ex)
        {
            PeriodStatusMessage = $"加载作息时间失败：{ex.Message}";
        }
    }

    // ── 数据备份区块 ─────────────────────────────────────────────

    /// <summary>备份结果提示：成功显示备份路径，失败显示失败提示（详情走错误弹窗）。</summary>
    [ObservableProperty]
    private string? _backupStatusMessage;

    /// <summary>立即备份数据：成功提示备份路径，失败弹出完整异常详情。</summary>
    /// <summary>备份列表（data/backup 下的目录备份与旧式扁平快照，按时间倒序）。</summary>
    [ObservableProperty]
    private ObservableCollection<BackupItem> _backups = new();

    /// <summary>当前选中的备份（列表点选即可，行内按钮也会把该行传进来）。</summary>
    [ObservableProperty]
    private BackupItem? _selectedBackup;

    /// <summary>重新读取备份列表。</summary>
    [RelayCommand]
    private void ReloadBackups()
    {
        Backups = new ObservableCollection<BackupItem>(_backupService.GetBackups());
        SelectedBackup = Backups.FirstOrDefault();
    }

    /// <summary>
    /// 恢复所选备份：键入库「恢复」才可执行，确认框注明"当前数据将被整体替换；恢复前已自动备份当前状态"。
    /// 服务层先做完整性校验（损坏则拒绝且库不动），再自动生成 pre-restore 备份，最后替换数据库并通知各页重查询。
    /// </summary>
    [RelayCommand]
    private void RestoreBackup(BackupItem? target)
    {
        target ??= SelectedBackup;
        if (target is null)
        {
            BackupStatusMessage = "请先在备份列表里选中要恢复的备份。";
            return;
        }

        var confirm = new ConfirmActionViewModel
        {
            Title = "恢复备份",
            Subject = $"目标备份：{target.Name}（{target.CreatedAtText}，{target.ReasonText}）",
            RequireTypedWord = true,
            ConfirmWord = "恢复",
            Notice = "当前数据将被整体替换；恢复前已自动备份当前状态。",
        };
        confirm.ImpactLines.Add("当前数据库与设置文件将被该备份的内容整体替换");
        confirm.RetainedLines.Add("恢复前的当前状态会自动备份为 pre-restore（可在列表中回退）");

        if (_dialogService.ShowDialog(confirm) != true)
        {
            return;
        }

        var result = _backupService.Restore(target);
        ReloadBackups();

        if (!result.Success)
        {
            BackupStatusMessage = result.ErrorMessage ?? "恢复失败。";
            _dialogService.ShowError(result.ErrorMessage ?? "恢复失败。", "恢复备份失败");
            return;
        }

        BackupStatusMessage = $"已恢复到 {target.Name}（{target.CreatedAtText}）；各页面数据已重新加载。";
    }

    /// <summary>删除所选备份（含旧式扁平快照）；删除前确认。</summary>
    [RelayCommand]
    private void DeleteBackup(BackupItem? target)
    {
        target ??= SelectedBackup;
        if (target is null)
        {
            BackupStatusMessage = "请先在备份列表里选中要删除的备份。";
            return;
        }

        if (!_dialogService.Confirm(
                $"确定删除备份「{target.Name}」吗？\n\n{target.CreatedAtText}　{target.ReasonText}　{target.SizeText}\n\n删除后无法恢复该备份。",
                "删除备份"))
        {
            return;
        }

        var result = _backupService.DeleteBackup(target);
        ReloadBackups();

        BackupStatusMessage = result.Success
            ? $"已删除备份 {target.Name}。"
            : result.ErrorMessage ?? "删除备份失败。";
    }

    [RelayCommand]
    private void BackupData()
    {
        BackupStatusMessage = "正在备份…";
        try
        {
            var backupPath = _backupService.Backup();
            var skipped = _backupService.LastSkippedBackupCount;
            BackupStatusMessage = skipped == 0
                ? $"备份完成：{backupPath}（保留最近 {_backupService.KeepCount} 份）"
                : $"备份完成：{backupPath}（保留最近 {_backupService.KeepCount} 份；另有 {skipped} 份旧备份被占用，未能清理）";
        }
        catch (Exception ex)
        {
            BackupStatusMessage = "备份失败，详见错误详情。";
            _dialogService.ShowError(ex.ToString(), "数据备份失败");
        }
    }

    private void LoadModules()
    {
        _isReloading = true;
        try
        {
            var items = new List<FeatureToggleItem>();
            foreach (var module in FeatureRegistry.Modules)
            {
                // 设置自身固定可见、不受开关控制
                if (module.IsAlwaysVisible)
                {
                    continue;
                }

                var item = new FeatureToggleItem(module, _settingsService.GetEffectiveVisibility(module));
                item.VisibilityChanged += OnItemVisibilityChanged;
                items.Add(item);
            }

            Modules = new ObservableCollection<FeatureToggleItem>(items);
        }
        finally
        {
            _isReloading = false;
        }
    }

    private void OnItemVisibilityChanged(object? sender, EventArgs e)
    {
        if (_isReloading || sender is not FeatureToggleItem item)
        {
            return;
        }

        // 即时生效：先落盘，再通知导航栏刷新
        _settingsService.SetModuleVisibility(item.Module.Id, item.IsVisible);
        _navigation.NotifyVisibleModulesChanged();
    }
}
