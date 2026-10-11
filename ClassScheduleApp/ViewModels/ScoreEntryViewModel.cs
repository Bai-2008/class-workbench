using System.Collections.ObjectModel;
using System.Globalization;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using ClassScheduleApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 综测记录页 ViewModel：标签页一"记录管理"、标签页二"学期汇总"。
/// 学期列表复用空闲时间统计的学期来源（StudentCourses.Semester 去重）；
/// 弹窗走已有 IDialogService，剪贴板复用 IClipboardService；不引用其它 ViewModel。
/// </summary>
public partial class ScoreEntryViewModel : ObservableObject
{
    private readonly IScoreEntryRepository _scoreRepository;
    private readonly IFreeTimeRepository _freeTimeRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IDialogService _dialogService;
    private readonly IClipboardService _clipboardService;
    private readonly IScoreExportService _exportService;
    private readonly IBackupService _backupService;

    private IReadOnlyList<Student> _students = new List<Student>();
    private IReadOnlyList<string> _categories = new List<string>();
    private bool _suppressReload;

    public ScoreEntryViewModel(
        IScoreEntryRepository scoreRepository,
        IFreeTimeRepository freeTimeRepository,
        IStudentRepository studentRepository,
        IDialogService dialogService,
        IClipboardService clipboardService,
        IScoreExportService exportService,
        IBackupService backupService)
    {
        _scoreRepository = scoreRepository;
        _freeTimeRepository = freeTimeRepository;
        _studentRepository = studentRepository;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        _exportService = exportService;
        _backupService = backupService;

        // 备份恢复 → 数据库已被整体替换，明细与汇总重新查询
        _backupService.Restored += (_, _) =>
        {
            _ = ReloadEntriesAsync();
            _ = ReloadSummaryAsync();
        };

        _suppressReload = true;
        try
        {
            SelectedTabIndex = 0;
        }
        finally
        {
            _suppressReload = false;
        }

        try
        {
            Task.Run(InitializeAsync).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            StatusMessage = $"初始化失败：{ex.Message}";
        }
    }

    // ── 标签页状态 ──────────────────────────────────────────────

    /// <summary>当前标签页（0 = 记录管理，1 = 学期汇总）。切走再切回时保留。</summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    // ── 标签页一：记录管理 ───────────────────────────────────────

    /// <summary>学期列表（来源与空闲时间统计一致）。</summary>
    [ObservableProperty]
    private ObservableCollection<string> _semesters = new();

    /// <summary>当前筛选学期（默认最新）。</summary>
    [ObservableProperty]
    private string? _selectedSemester;

    /// <summary>学生过滤下拉（全部学生 + 全班）。</summary>
    [ObservableProperty]
    private ObservableCollection<ScoreFilterOption> _studentFilters = new();

    /// <summary>当前学生过滤。</summary>
    [ObservableProperty]
    private ScoreFilterOption? _selectedStudentFilter;

    /// <summary>类别过滤下拉（全部类别 + 预设 + 历史自定义）。</summary>
    [ObservableProperty]
    private ObservableCollection<ScoreFilterOption> _categoryFilters = new();

    /// <summary>当前类别过滤。</summary>
    [ObservableProperty]
    private ScoreFilterOption? _selectedCategoryFilter;

    /// <summary>日期范围起（可空 = 不限）。</summary>
    [ObservableProperty]
    private DateTime? _rangeStart;

    /// <summary>日期范围止（可空 = 不限）。</summary>
    [ObservableProperty]
    private DateTime? _rangeEnd;

    /// <summary>记录集合。</summary>
    [ObservableProperty]
    private ObservableCollection<ScoreEntryInfo> _entries = new();

    /// <summary>当前选中行（双击编辑用）。</summary>
    [ObservableProperty]
    private ScoreEntryInfo? _selectedEntry;

    // ── 标签页二：学期汇总 ───────────────────────────────────────

    /// <summary>汇总学期（默认最新）。</summary>
    [ObservableProperty]
    private string? _summarySemester;

    /// <summary>学生汇总（按总分降序）。</summary>
    [ObservableProperty]
    private ObservableCollection<StudentScoreSummary> _summaries = new();

    /// <summary>操作提示。</summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>添加记录：打开编辑弹窗；多选学生则批量插入；命中疑似重复时先 Confirm。</summary>
    [RelayCommand]
    private async Task AddEntryAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedSemester))
        {
            StatusMessage = "还没有可用学期：学期取自选课记录的学期，请先在学生选课里建立学期。";
            return;
        }

        var viewModel = new ScoreEditViewModel();
        viewModel.Initialize(null, _students, _categories, SelectedSemester);

        if (_dialogService.ShowDialog(viewModel) != true)
        {
            return;
        }

        var studentIds = viewModel.SelectedStudentIds;
        var draft = viewModel.BuildDraft(string.Empty);

        // 疑似重复：同学期下已有完全相同的 (类别, 分数, 说明)
        var duplicates = await _scoreRepository.FindDuplicateStudentIdsAsync(
            draft.Semester, studentIds, draft.Category, draft.Score, draft.Description);

        if (duplicates.Count > 0)
        {
            var names = new List<string>();
            foreach (var student in _students)
            {
                foreach (var id in duplicates)
                {
                    if (student.Id == id)
                    {
                        names.Add(student.Name);
                        break;
                    }
                }
            }

            var message = $"以下学生在本学期已有完全相同的记录（类别「{draft.Category}」、分数 {draft.Score.ToString("0.##", CultureInfo.CurrentCulture)}、说明「{draft.Description}」）：\n\n" +
                          string.Join("、", names) +
                          "\n\n仍要继续保存吗？";
            if (!_dialogService.Confirm(message, "可能重复的记录"))
            {
                StatusMessage = "已取消保存（检测到疑似重复记录）。";
                return;
            }
        }

        var result = await _scoreRepository.AddBatchAsync(studentIds, draft);
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "保存失败。";
            return;
        }

        await ReloadEntriesAsync();
        await ReloadSummaryAsync();
        StatusMessage = $"已添加 {studentIds.Count} 条综测记录：{draft.Semester} {draft.Category} {draft.Score.ToString("0.##", CultureInfo.CurrentCulture)}。";
    }

    /// <summary>编辑：双击行或点行尾"编辑"按钮；不传参时用当前选中行。</summary>
    [RelayCommand]
    private async Task EditEntryAsync(ScoreEntryInfo? entry)
    {
        var target = entry ?? SelectedEntry;
        if (target is null)
        {
            StatusMessage = "请先选中一行记录。";
            return;
        }

        var viewModel = new ScoreEditViewModel();
        viewModel.Initialize(target, _students, _categories, target.Semester);

        if (_dialogService.ShowDialog(viewModel) != true)
        {
            return;
        }

        var draft = viewModel.BuildDraft(string.Empty);
        var result = await _scoreRepository.UpdateAsync(target.Id, draft);
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "保存失败。";
            return;
        }

        await ReloadEntriesAsync();
        await ReloadSummaryAsync();
        StatusMessage = $"已修改综测记录：{target.StudentName} {draft.Category} {draft.Score.ToString("0.##", CultureInfo.CurrentCulture)}。";
    }

    /// <summary>
    /// 批量删除所选综测记录：确认框注明所选条数与「删除后不可恢复」，执行前自动备份；
    /// 逐条删除后刷新明细与汇总，状态栏给出备份路径。
    /// </summary>
    [RelayCommand]
    private async Task DeleteSelectedAsync(IReadOnlyList<ScoreEntryInfo>? entries)
    {
        var targets = entries is null
            ? new List<ScoreEntryInfo>()
            : new List<ScoreEntryInfo>(entries);

        if (targets.Count == 0)
        {
            StatusMessage = "请先在明细表里选中要删除的综测记录（可按住 Ctrl 或 Shift 多选）。";
            return;
        }

        var total = 0m;
        var students = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in targets)
        {
            total += entry.Score;
            students.Add(entry.StudentName);
        }

        var confirm = new ConfirmActionViewModel
        {
            Title = "批量删除综测记录",
            Subject = $"共选中 {targets.Count} 条综测记录",
            Notice = "综测记录删除后不可恢复（学生、课程与考勤记录不受影响）。",
        };
        confirm.ImpactLines.Add($"所选 {targets.Count} 条综测记录，涉及 {students.Count} 名学生，分数合计 {total:0.##}");
        confirm.RetainedLines.Add("学生、课程、考勤与班费记录（不受影响）");

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
        foreach (var entry in targets)
        {
            var result = await _scoreRepository.DeleteAsync(entry.Id);
            if (result.Success)
            {
                deleted++;
            }
            else
            {
                failures.Add(result.ErrorMessage ?? $"记录 #{entry.Id} 删除失败");
            }
        }

        await ReloadEntriesAsync();
        await ReloadSummaryAsync();

        StatusMessage = $"已删除 {deleted} 条综测记录" +
            (failures.Count == 0 ? "。" : $"；失败 {failures.Count} 条：{string.Join("；", failures)}") +
            "。" + backupNote;
    }

    /// <summary>删除：先走确认框。</summary>
    [RelayCommand]
    private async Task DeleteEntryAsync(ScoreEntryInfo? entry)
    {
        if (entry is null)
        {
            StatusMessage = "请先选中一行记录。";
            return;
        }

        var message = $"确定删除这条综测记录吗？\n\n{entry.StudentText} {entry.Semester}\n{entry.Category} {entry.ScoreText}\n{entry.Description}";
        if (!_dialogService.Confirm(message, "删除综测记录"))
        {
            return;
        }

        var result = await _scoreRepository.DeleteAsync(entry.Id);
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "删除失败。";
            return;
        }

        await ReloadEntriesAsync();
        await ReloadSummaryAsync();
        StatusMessage = $"已删除一条综测记录（{entry.StudentName} {entry.Category} {entry.ScoreText}）。";
    }

    /// <summary>复制汇总：Tab 分隔文本写入剪贴板，粘贴进 Excel 列对齐。</summary>
    [RelayCommand]
    private void CopySummary()
    {
        if (Summaries.Count == 0)
        {
            StatusMessage = "当前没有可复制的汇总数据。";
            return;
        }

        var text = ScoreSummaryText.BuildTabSeparated(Summaries);
        StatusMessage = _clipboardService.TrySetText(text)
            ? $"已复制 {Summaries.Count} 名学生的汇总（Tab 分隔，可直接粘贴进 Excel）。"
            : "复制失败：剪贴板被其它程序占用，请稍后重试。";
    }

    /// <summary>导出 Excel：Sheet1 汇总交叉表、Sheet2 明细记录。</summary>
    [RelayCommand]
    private async Task ExportSummaryAsync()
    {
        if (string.IsNullOrWhiteSpace(SummarySemester))
        {
            StatusMessage = "请先选择要导出的学期。";
            return;
        }

        var entries = await _scoreRepository.GetEntriesAsync(SummarySemester);
        StatusMessage = "正在导出…";
        var result = await _exportService.ExportAsync(SummarySemester, Summaries, entries);
        StatusMessage = result.Success
            ? $"已导出：{result.FilePath}"
            : result.ErrorMessage ?? "导出失败。";
    }

    partial void OnSelectedSemesterChanged(string? value)
    {
        if (!_suppressReload)
        {
            _ = ReloadEntriesAsync();
        }
    }

    partial void OnSelectedStudentFilterChanged(ScoreFilterOption? value)
    {
        if (!_suppressReload)
        {
            _ = ReloadEntriesAsync();
        }
    }

    partial void OnSelectedCategoryFilterChanged(ScoreFilterOption? value)
    {
        if (!_suppressReload)
        {
            _ = ReloadEntriesAsync();
        }
    }

    partial void OnRangeStartChanged(DateTime? value)
    {
        if (!_suppressReload)
        {
            _ = ReloadEntriesAsync();
        }
    }

    partial void OnRangeEndChanged(DateTime? value)
    {
        if (!_suppressReload)
        {
            _ = ReloadEntriesAsync();
        }
    }

    partial void OnSummarySemesterChanged(string? value)
    {
        if (!_suppressReload)
        {
            _ = ReloadSummaryAsync();
        }
    }

    private async Task InitializeAsync()
    {
        _students = await _studentRepository.GetAllStudentsAsync();
        _categories = await _scoreRepository.GetCategoriesAsync();

        var semesters = await _freeTimeRepository.GetSemestersAsync();

        _suppressReload = true;
        try
        {
            Semesters = new ObservableCollection<string>(semesters);
            // 集合刚重建：值相同时不会发通知，需显式通知，否则下拉框显示空白
            var targetSemester = Semesters.Count > 0 ? Semesters[0] : null;
            if (SelectedSemester != targetSemester)
            {
                SelectedSemester = targetSemester;
            }
            else
            {
                OnPropertyChanged(nameof(SelectedSemester));
            }

            if (SummarySemester != targetSemester)
            {
                SummarySemester = targetSemester;
            }
            else
            {
                OnPropertyChanged(nameof(SummarySemester));
            }

            var studentOptions = new List<ScoreFilterOption> { new("全部学生") };
            foreach (var student in _students)
            {
                studentOptions.Add(new ScoreFilterOption($"{student.Name}（{student.StudentNo}）", student.Id));
            }

            StudentFilters = new ObservableCollection<ScoreFilterOption>(studentOptions);
            SelectedStudentFilter = StudentFilters[0];

            var categoryOptions = new List<ScoreFilterOption> { new("全部类别") };
            foreach (var category in _categories)
            {
                categoryOptions.Add(new ScoreFilterOption(category, null, category));
            }

            CategoryFilters = new ObservableCollection<ScoreFilterOption>(categoryOptions);
            SelectedCategoryFilter = CategoryFilters[0];
        }
        finally
        {
            _suppressReload = false;
        }

        await ReloadEntriesAsync();
        await ReloadSummaryAsync();
    }

    private async Task ReloadEntriesAsync()
    {
        try
        {
            DateOnly? start = RangeStart.HasValue ? DateOnly.FromDateTime(RangeStart.Value) : null;
            DateOnly? end = RangeEnd.HasValue ? DateOnly.FromDateTime(RangeEnd.Value) : null;

            var entries = await _scoreRepository.GetEntriesAsync(
                SelectedSemester,
                SelectedStudentFilter?.StudentId,
                SelectedCategoryFilter?.Category,
                start,
                end);

            Entries = new ObservableCollection<ScoreEntryInfo>(entries);
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载综测记录失败：{ex.Message}";
        }
    }

    private async Task ReloadSummaryAsync()
    {
        if (string.IsNullOrWhiteSpace(SummarySemester))
        {
            Summaries = new ObservableCollection<StudentScoreSummary>();
            return;
        }

        try
        {
            var summaries = await _scoreRepository.GetSummaryAsync(SummarySemester);
            Summaries = new ObservableCollection<StudentScoreSummary>(summaries);
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载学期汇总失败：{ex.Message}";
        }
    }
}
