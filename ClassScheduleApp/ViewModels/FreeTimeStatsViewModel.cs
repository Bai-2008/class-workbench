using System.Collections.ObjectModel;
using ClassScheduleApp.Repositories;
using ClassScheduleApp.Services;
using ClassScheduleApp.Services.ScheduleImport;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 空闲时间统计页 ViewModel：按学期 + 周次统计全班无课时段，
/// 选中格子后展示该时段空闲/有课两份名单，并支持复制与导出。
/// 只做状态编排，查询全部走仓储。
/// </summary>
public partial class FreeTimeStatsViewModel : ObservableObject
{
    /// <summary>节次对个数：1-2/3-4/5-6/7-8/9-10/11-12。</summary>
    private const int SlotCount = 6;

    private readonly IFreeTimeRepository _repository;
    private readonly IClipboardService _clipboardService;
    private readonly IExportService _exportService;

    /// <summary>当前矩阵原始数据（导出用）。</summary>
    private IReadOnlyList<ClassFreeCell> _currentCells = Array.Empty<ClassFreeCell>();

    private readonly IPeriodTimeService _periodTimeService;
    private readonly IScheduleAdjustmentService _adjustmentService;
    private readonly ISemesterService _semesterService;
    private readonly IScheduleImportService _importService;
    private readonly IBackupService _backupService;

    /// <summary>课表导入落地后矩阵即时刷新（导入会重建学生的课程关联，直接影响全班占用）。</summary>
    private void OnScheduleImported(object? sender, EventArgs e)
    {
        _ = LoadMatrixAsync();
    }

    /// <summary>调休补课模块保存/删除后，矩阵数值即时刷新（放假/调休/停课/补课都会影响全班空闲）。</summary>
    private void OnAdjustmentsChanged(object? sender, EventArgs e)
    {
        _ = LoadMatrixAsync();
    }

    /// <summary>初始化期间抑制属性变更触发的重复加载。</summary>
    private bool _suppressReload;

    private void OnPeriodTimesChanged(object? sender, EventArgs e)
    {
        _ = LoadMatrixAsync();
    }

    public FreeTimeStatsViewModel(
        IFreeTimeRepository repository,
        IClipboardService clipboardService,
        IExportService exportService,
        IPeriodTimeService periodTimeService,
        IScheduleAdjustmentService adjustmentService,
        ISemesterService semesterService,
        IScheduleImportService importService,
        IBackupService backupService)
    {
        _repository = repository;
        _clipboardService = clipboardService;
        _exportService = exportService;
        _periodTimeService = periodTimeService;
        _adjustmentService = adjustmentService;
        _semesterService = semesterService;
        _importService = importService;
        _backupService = backupService;

        // 备份恢复 → 数据库已被整体替换，空闲矩阵重新查询
        _backupService.Restored += (_, _) => { _ = LoadMatrixAsync(); };

        // 调休补课变更 → 矩阵刷新
        _adjustmentService.Changed += OnAdjustmentsChanged;

        // 课表导入落地 → 矩阵刷新（否则导入后仍显示旧统计）
        _importService.Imported += OnScheduleImported;

        // 设置页保存作息时间后，矩阵行标签（节次对 + 时间范围）即时刷新
        _periodTimeService.Changed += OnPeriodTimesChanged;

        _suppressReload = true;
        try
        {
            var weeks = new List<int>();
            for (var week = 1; week <= 25; week++)
            {
                weeks.Add(week);
            }

            WeekOptions = new ObservableCollection<int>(weeks);
            SelectedWeek = 1;
        }
        finally
        {
            _suppressReload = false;
        }

        // 首屏加载放在线程池执行并同步等待：避免在线程上阻塞异步调用造成死锁
        try
        {
            Task.Run(InitializeAsync).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            StatusMessage = $"初始化失败：{ex.Message}";
        }
    }

    /// <summary>学期下拉（来自 StudentCourses.Semester 去重，最新的在前）。</summary>
    [ObservableProperty]
    private ObservableCollection<string> _semesters = new();

    /// <summary>当前学期。</summary>
    [ObservableProperty]
    private string? _selectedSemester;

    /// <summary>周次可选项 1~25。</summary>
    [ObservableProperty]
    private ObservableCollection<int> _weekOptions = new();

    /// <summary>当前周次。</summary>
    [ObservableProperty]
    private int _selectedWeek = 1;

    /// <summary>矩阵（6 行 × 7 列 = 42 格）。</summary>
    [ObservableProperty]
    private ObservableCollection<ClassFreeRowViewModel> _matrixRows = new();

    /// <summary>当前选中的格子。</summary>
    [ObservableProperty]
    private ClassFreeCellViewModel? _selectedCell;

    /// <summary>选中格的标题，如 周一 1-2节。</summary>
    [ObservableProperty]
    private string _selectedCellTitle = "未选择时段";

    /// <summary>该时段空闲名单。</summary>
    [ObservableProperty]
    private ObservableCollection<FreeTimeNameItem> _freeStudents = new();

    /// <summary>该时段有课名单（含课程名与教室）。</summary>
    [ObservableProperty]
    private ObservableCollection<FreeTimeNameItem> _busyStudents = new();

    /// <summary>操作结果提示（复制/导出）。</summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>点击格子。</summary>
    [RelayCommand]
    private void SelectCell(ClassFreeCellViewModel? cell)
    {
        if (cell is null)
        {
            return;
        }

        foreach (var row in MatrixRows)
        {
            foreach (var item in row.Cells)
            {
                item.IsSelected = ReferenceEquals(item, cell);
            }
        }

        SelectedCell = cell;
    }

    /// <summary>复制空闲名单。</summary>
    [RelayCommand]
    private void CopyFreeStudents()
    {
        var cell = SelectedCell;
        if (cell is null)
        {
            StatusMessage = "请先点击一个时段格子。";
            return;
        }

        var lines = new List<string>
        {
            $"空闲名单 {SelectedSemester} 第{SelectedWeek}周 {cell.WeekdayText} {cell.PeriodText}（共 {cell.FreeCount}/{cell.TotalCount} 人空闲）",
        };

        foreach (var item in FreeStudents)
        {
            lines.Add(item.Name);
        }

        CopyToClipboard(lines, "空闲名单");
    }

    /// <summary>复制有课名单（含课程名与教室）。</summary>
    [RelayCommand]
    private void CopyBusyStudents()
    {
        var cell = SelectedCell;
        if (cell is null)
        {
            StatusMessage = "请先点击一个时段格子。";
            return;
        }

        var lines = new List<string>
        {
            $"有课名单 {SelectedSemester} 第{SelectedWeek}周 {cell.WeekdayText} {cell.PeriodText}",
        };

        foreach (var item in BusyStudents)
        {
            lines.Add(string.IsNullOrEmpty(item.Detail) ? item.Name : $"{item.Name}  {item.Detail}");
        }

        CopyToClipboard(lines, "有课名单");
    }

    /// <summary>导出本周无课表（xlsx）。</summary>
    [RelayCommand]
    private async Task ExportWeekAsync()
    {
        var semester = SelectedSemester;
        if (string.IsNullOrEmpty(semester))
        {
            StatusMessage = "没有可导出的学期数据。";
            return;
        }

        if (_currentCells.Count == 0)
        {
            StatusMessage = "当前没有可导出的矩阵数据。";
            return;
        }

        StatusMessage = "正在导出…";
        var result = await _exportService.ExportFreeTimeGridAsync(semester, SelectedWeek, _currentCells);
        StatusMessage = result.Success
            ? $"已导出：{result.FilePath}"
            : result.ErrorMessage ?? "导出失败。";
    }

    partial void OnSelectedSemesterChanged(string? value)
    {
        if (!_suppressReload)
        {
            _ = ReloadMatrixAsync();
        }
    }

    partial void OnSelectedWeekChanged(int value)
    {
        if (!_suppressReload)
        {
            _ = ReloadMatrixAsync();
        }
    }

    partial void OnSelectedCellChanged(ClassFreeCellViewModel? value)
    {
        if (value is not null)
        {
            _ = LoadCellDetailAsync(value);
        }
    }

    private async Task InitializeAsync()
    {
        var semesters = await _repository.GetSemestersAsync();

        // 作息时间（行标签显示时间范围）；失败时降级只显示节次对
        try
        {
            await _periodTimeService.LoadAsync();
            await _semesterService.LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"读取作息时间失败：{ex.Message}";
        }

        _suppressReload = true;
        try
        {
            Semesters = new ObservableCollection<string>(semesters);
            // 默认最新学期（仓储按倒序返回）；集合刚重建，值相同时不会发通知，需显式通知一次
            var targetSemester = Semesters.Count > 0 ? Semesters[0] : null;
            if (SelectedSemester != targetSemester)
            {
                SelectedSemester = targetSemester;
            }
            else
            {
                OnPropertyChanged(nameof(SelectedSemester));
            }

            // 已配置学期起始日 → 默认定位到今天所在周（未配置则维持第 1 周）
            if (SelectedSemester is not null)
            {
                var todayWeek = _semesterService.GetWeekOf(SelectedSemester, DateOnly.FromDateTime(DateTime.Today));
                if (todayWeek.HasValue && WeekOptions.Contains(todayWeek.Value))
                {
                    SelectedWeek = todayWeek.Value;
                }
            }
        }
        finally
        {
            _suppressReload = false;
        }

        await LoadMatrixAsync();
    }

    private async Task ReloadMatrixAsync()
    {
        // 切换学期/周次后原有的选中格已失效
        SelectedCell = null;
        SelectedCellTitle = "未选择时段";
        FreeStudents = new ObservableCollection<FreeTimeNameItem>();
        BusyStudents = new ObservableCollection<FreeTimeNameItem>();

        await LoadMatrixAsync();
    }

    private async Task LoadMatrixAsync()
    {
        var semester = SelectedSemester;
        if (string.IsNullOrEmpty(semester))
        {
            _currentCells = Array.Empty<ClassFreeCell>();
            MatrixRows = new ObservableCollection<ClassFreeRowViewModel>();
            return;
        }

        try
        {
            var cells = await _repository.GetClassFreeMatrixAsync(semester, SelectedWeek);
            _currentCells = cells;

            // 行槽位按返回的格子动态确定（作息节数可配置，故不再写死 6 行）
            var slotCount = 0;
            foreach (var cell in cells)
            {
                if (cell.SlotIndex + 1 > slotCount)
                {
                    slotCount = cell.SlotIndex + 1;
                }
            }

            var rows = new List<ClassFreeRowViewModel>();
            for (var slot = 0; slot < slotCount; slot++)
            {
                var rowCells = new List<ClassFreeCellViewModel>();
                var slotText = string.Empty;
                var slotStartPeriod = 0;
                var slotEndPeriod = 0;

                foreach (var cell in cells)
                {
                    if (cell.SlotIndex != slot)
                    {
                        continue;
                    }

                    slotText = cell.PeriodText;
                    slotStartPeriod = cell.StartPeriod;
                    slotEndPeriod = cell.EndPeriod;
                    rowCells.Add(new ClassFreeCellViewModel(cell));
                }

                // 行标签 = 节次对 + 时间范围（如 "3-4节 10:25-12:00"）；作息未载入时只显示节次对
                var range = _periodTimeService.GetRangeText(slotStartPeriod, slotEndPeriod);
                var slotLabel = slotText.Length > 0 && range.Length > 0 ? $"{slotText} {range}" : slotText;

                // 按周一 → 周日排序（仓储按天升序返回，这里再稳一手）
                rowCells.Sort(static (left, right) => left.DayOfWeek.CompareTo(right.DayOfWeek));
                rows.Add(new ClassFreeRowViewModel(slotLabel, rowCells));
            }

            MatrixRows = new ObservableCollection<ClassFreeRowViewModel>(rows);
            StatusMessage = $"{semester} 第{SelectedWeek}周：共 {rows.Count * 7} 个时段格子。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载矩阵失败：{ex.Message}";
        }
    }

    private async Task LoadCellDetailAsync(ClassFreeCellViewModel cell)
    {
        var semester = SelectedSemester;
        if (string.IsNullOrEmpty(semester))
        {
            return;
        }

        SelectedCellTitle = $"{cell.WeekdayText} {cell.PeriodText} 详情";

        try
        {
            var free = await _repository.GetFreeStudentsAsync(
                semester, SelectedWeek, cell.DayOfWeek, cell.StartPeriod, cell.EndPeriod);
            var busy = await _repository.GetBusyStudentsAsync(
                semester, SelectedWeek, cell.DayOfWeek, cell.StartPeriod, cell.EndPeriod);

            var freeItems = new List<FreeTimeNameItem>();
            foreach (var student in free)
            {
                freeItems.Add(new FreeTimeNameItem(student.StudentNo, student.Name, null));
            }

            var busyItems = new List<FreeTimeNameItem>();
            foreach (var item in busy)
            {
                busyItems.Add(new FreeTimeNameItem(item.StudentNo, item.StudentName, $"{item.CourseName}  {item.Location}"));
            }

            FreeStudents = new ObservableCollection<FreeTimeNameItem>(freeItems);
            BusyStudents = new ObservableCollection<FreeTimeNameItem>(busyItems);
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载时段详情失败：{ex.Message}";
        }
    }

    private void CopyToClipboard(List<string> lines, string what)
    {
        var text = string.Join(Environment.NewLine, lines);
        StatusMessage = _clipboardService.TrySetText(text)
            ? $"已复制{what}（{lines.Count - 1} 条）。"
            : $"复制{what}失败：剪贴板被其它程序占用，请重试。";
    }
}
