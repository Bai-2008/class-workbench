using System.Collections.ObjectModel;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using ClassScheduleApp.Services;
using ClassScheduleApp.Services.ScheduleImport;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 学生课表页 ViewModel：学生列表、当前学生、课表数据与课表页的交互（新增/编辑/删除学生、选课、课程管理）。
/// 只做状态编排，不写 LINQ 查询（查询全部在 Repositories 层），也不做服务定位。
/// 从 MainViewModel 迁出：外壳只负责导航，页面业务回归各自模块。
/// </summary>
public partial class StudentScheduleViewModel : ObservableObject
{
    private readonly IStudentRepository _studentRepository;
    private readonly IStudentAdminRepository _studentAdminRepository;
    private readonly IDialogService _dialogService;
    private readonly ISemesterService _semesterService;
    private readonly IPeriodTimeService _periodTimeService;
    private readonly ICourseAdminRepository _courseAdminRepository;
    private readonly IScheduleAdjustmentService _adjustmentService;
    private readonly IScheduleDayCalculator _scheduleDayCalculator;
    private readonly IScheduleImportService _importService;
    private readonly IStudentImportService _studentImportService;
    private readonly IBackupService _backupService;

    /// <summary>课表导入覆盖后，当前学生的课表即时刷新（含网格与列表）。</summary>
    private void OnScheduleImported(object? sender, EventArgs e)
    {
        _ = LoadScheduleAsync(SelectedStudent);
    }

    /// <summary>当前周的调整生效计划（周一→周日；模块禁用或起始日缺失时为空集合 = 不叠加）。</summary>
    private IReadOnlyList<AdjustmentDayPlan> _dayPlans = Array.Empty<AdjustmentDayPlan>();

    /// <summary>课程 Id → 课程信息（补课格展示课程名/教师/教室用）。</summary>
    private readonly Dictionary<int, (string Name, string Teacher, string Location)> _courseInfo = new();

    /// <summary>当前学生在该学期的全部课表条目（未按周次过滤）。</summary>
    private List<StudentScheduleItem> _allScheduleItems = new();

    /// <summary>课表页当前使用的学期（取最新学期；学期设置也按它读取）。</summary>
    private string? _semester;

    public StudentScheduleViewModel(
        IStudentRepository studentRepository,
        IStudentAdminRepository studentAdminRepository,
        IDialogService dialogService,
        ISemesterService semesterService,
        IPeriodTimeService periodTimeService,
        ICourseAdminRepository courseAdminRepository,
        IScheduleAdjustmentService adjustmentService,
        IScheduleDayCalculator scheduleDayCalculator,
        IScheduleImportService importService,
        IStudentImportService studentImportService,
        IBackupService backupService)
    {
        _studentRepository = studentRepository;
        _studentAdminRepository = studentAdminRepository;
        _dialogService = dialogService;
        _semesterService = semesterService;
        _periodTimeService = periodTimeService;
        _courseAdminRepository = courseAdminRepository;
        _adjustmentService = adjustmentService;
        _scheduleDayCalculator = scheduleDayCalculator;
        _importService = importService;
        _studentImportService = studentImportService;
        _backupService = backupService;

        // 课表导入落地 → 当前学生课表刷新
        _importService.Imported += OnScheduleImported;

        // 备份恢复 → 数据库已被整体替换，当前学生课表重新查询
        _backupService.Restored += (_, _) => { _ = LoadScheduleAsync(SelectedStudent); };

        // 调休补课模块保存/删除后，课表叠加即时刷新
        _adjustmentService.Changed += OnAdjustmentsChanged;

        // 设置页保存作息时间后，课表行头与图例即时刷新
        _periodTimeService.Changed += OnPeriodTimesChanged;
    }

    /// <summary>课表展示形态：网格（默认）/ 列表；列表原样保留，切换状态随页面保留。</summary>
    public IReadOnlyList<string> ScheduleViews { get; } = new[] { "网格", "列表" };

    /// <summary>当前展示形态。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGridView))]
    [NotifyPropertyChangedFor(nameof(IsListView))]
    private string _scheduleView = "网格";

    /// <summary>周视图网格的全部可视项（表头 / 节次标签 / 分隔 / 单元格）。</summary>
    [ObservableProperty]
    private ObservableCollection<WeekGridItemViewModel> _weekGridItems = new();

    /// <summary>是否网格视图。</summary>
    public bool IsGridView => ScheduleView == "网格";

    /// <summary>是否列表视图。</summary>
    public bool IsListView => ScheduleView == "列表";

    partial void OnScheduleViewChanged(string value)
    {
        RefreshWeekGrid();
    }

    /// <summary>节次 → 网格行号（第 4 节后、第 8 节后各插入一行分隔，节数不足时不留分隔）。</summary>
    private int GridRowOf(int period)
    {
        var count = _periodTimeService.PeriodCount;
        var row = period;
        if (count > 4 && period > 4)
        {
            row++;
        }

        if (count > 8 && period > 8)
        {
            row++;
        }

        return row;
    }

    /// <summary>取某天（1=周一）的调整生效计划；模块禁用或无调整时返回 null。</summary>
    private AdjustmentDayPlan? GetPlan(int dayOfWeek)
    {
        return _dayPlans.Count >= dayOfWeek ? _dayPlans[dayOfWeek - 1] : null;
    }

    private (string Name, string Teacher, string Location) CourseInfoOf(int? courseId)
    {
        if (courseId is not null && _courseInfo.TryGetValue(courseId.Value, out var info))
        {
            return info;
        }

        return courseId is null ? (string.Empty, string.Empty, string.Empty) : ($"课程#{courseId.Value}", string.Empty, string.Empty);
    }

    /// <summary>按当前周次数据重建周视图网格（连堂课跨行合并，冲突同格纵排，并叠加调休补课）。</summary>
    private void RefreshWeekGrid()
    {
        var items = new List<WeekGridItemViewModel>();

        // 表头行：复用列表视图的日期表头；调整生效时带「放假 / 上周X课」标记
        var headers = DayHeaders;
        for (var index = 0; index < headers.Count && index < 7; index++)
        {
            var plan = GetPlan(index + 1);
            items.Add(new WeekGridHeaderItem(headers[index], index + 1, plan?.MarkText ?? string.Empty));
        }

        // 节次标签 + 午休/晚休分隔（作息时间同一数据源；节数可配置）
        var periodCount = _periodTimeService.PeriodCount;
        for (var period = 1; period <= periodCount; period++)
        {
            var row = GridRowOf(period);
            items.Add(new WeekGridLabelItem(period, $"第 {period} 节", _periodTimeService.GetPeriodRangeText(period), row));

            if (period == 4 && periodCount > 4)
            {
                items.Add(new WeekGridSeparatorItem("午休", row + 1));
            }
            else if (period == 8 && periodCount > 8)
            {
                items.Add(new WeekGridSeparatorItem("晚休", row + 1));
            }
        }

        // 逐列渲染：放假日置空并标「放假」；调休日显示 FollowWeekday 对应课表并标「上周X课」
        for (var day = 1; day <= 7; day++)
        {
            var plan = GetPlan(day);
            var isHoliday = plan?.IsHoliday == true;

            var byPeriod = new Dictionary<int, List<WeekGridCourseEntry>>();
            var endByPeriod = new Dictionary<int, int>();

            // 叠加计算走共享内核（放假 / 停课 / 补课 / 调休），本页不再自行判定
            var makeUpSlots = new List<MakeUpSlot>();
            if (plan is not null)
            {
                foreach (var makeup in plan.MakeUps)
                {
                    if (makeup.StartPeriod is null || makeup.EndPeriod is null)
                    {
                        continue;
                    }

                    var info = CourseInfoOf(makeup.CourseId);
                    makeUpSlots.Add(new MakeUpSlot
                    {
                        CourseId = makeup.CourseId,
                        CourseName = info.Name,
                        Teacher = info.Teacher,
                        Location = string.IsNullOrWhiteSpace(makeup.NewLocation) ? info.Location : makeup.NewLocation!,
                        StartPeriod = makeup.StartPeriod,
                        EndPeriod = makeup.EndPeriod,
                    });
                }
            }

            var daySlots = ScheduleItems.Select(item => new DayScheduleSlot
            {
                CourseId = item.CourseId,
                CourseName = item.CourseName,
                Teacher = item.Teacher,
                Location = item.Location,
                DayOfWeek = item.DayOfWeek,
                StartPeriod = item.StartPeriod,
                EndPeriod = item.EndPeriod,
                ParityText = item.ParityText,
            });

            var dayResult = _scheduleDayCalculator.CalculateDay(day, plan, daySlots, makeUpSlots);
            foreach (var entry in dayResult.Entries)
            {
                AddEntry(byPeriod, endByPeriod, entry.StartPeriod, entry.EndPeriod,
                    new WeekGridCourseEntry(entry.CourseId, entry.CourseName, entry.Teacher, entry.Location, entry.IsCancelled, entry.IsMakeUp)
                    {
                        ParityText = entry.ParityText,
                    });
            }

            for (var period = 1; period <= periodCount; period++)
            {
                var entries = byPeriod.TryGetValue(period, out var list) ? list : new List<WeekGridCourseEntry>();
                var maxEnd = endByPeriod.TryGetValue(period, out var end) ? end : period;
                var coveredByMerged = false;

                foreach (var pair in endByPeriod)
                {
                    if (pair.Key < period && period <= pair.Value)
                    {
                        coveredByMerged = true;
                    }
                }

                // 被上一节的连堂课覆盖、且此处没有新课：不重复画（避免遮住跨行格）
                if (coveredByMerged && entries.Count == 0)
                {
                    continue;
                }

                if (maxEnd > periodCount)
                {
                    maxEnd = periodCount;
                }

                items.Add(new WeekGridCellItem(day, period, maxEnd, entries, GridRowOf(period), day, isHoliday));
            }
        }

        WeekGridItems = new ObservableCollection<WeekGridItemViewModel>(items);
    }

    /// <summary>把一个课程时段加入「起始节 → 条目 / 结束节」两张表。</summary>
    private static void AddEntry(
        Dictionary<int, List<WeekGridCourseEntry>> byPeriod,
        Dictionary<int, int> endByPeriod,
        int startPeriod,
        int endPeriod,
        WeekGridCourseEntry entry)
    {
        if (!byPeriod.TryGetValue(startPeriod, out var list))
        {
            list = new List<WeekGridCourseEntry>();
            byPeriod[startPeriod] = list;
        }

        list.Add(entry);

        if (!endByPeriod.TryGetValue(startPeriod, out var current) || endPeriod > current)
        {
            endByPeriod[startPeriod] = endPeriod;
        }
    }

    /// <summary>载入当前学期的调整计划（模块禁用 / 起始日缺失时为 0 条，相当于不叠加）。</summary>
    private async Task RefreshAdjustmentPlansAsync()
    {
        try
        {
            // 该周 7 天计划统一由共享计算服务给出（调整表读取收敛于该服务）
            _dayPlans = await _scheduleDayCalculator.GetWeekDayPlansAsync(_semester, SelectedWeek);
            var available = _scheduleDayCalculator.IsConfigured(_semester);

            if (available)
            {
                _courseInfo.Clear();
                var courses = await _courseAdminRepository.GetAllCoursesAsync();
                foreach (var course in courses)
                {
                    _courseInfo[course.Id] = (course.CourseName, course.Teacher, course.Location);
                }
            }
        }
        catch (Exception ex)
        {
            _dayPlans = Array.Empty<AdjustmentDayPlan>();
            WeekHint = $"读取调休补课数据失败：{ex.Message}";
        }

        RefreshWeekGrid();
    }

    /// <summary>单击有课格：同格多门课时弹出候选选择（复用通用弹窗壳）。</summary>
    [RelayCommand]
    private async Task SelectCellCourseAsync(WeekGridCellItem? cell)
    {
        if (cell is null || !cell.HasConflict)
        {
            return;
        }

        var options = new List<CourseChoiceItem>();
        var timeText = _periodTimeService.GetRangeText(cell.StartPeriod, cell.EndPeriod);
        foreach (var entry in cell.Entries)
        {
            options.Add(new CourseChoiceItem(entry.CourseId, entry.CourseName + (entry.HasMark ? $"（{entry.MarkText}）" : string.Empty), entry.Teacher, entry.Location, timeText));
        }

        var viewModel = new CourseChoiceViewModel($"选择课程（{cell.Entries.Count} 门）", options);
        if (_dialogService.ShowDialog(viewModel) != true || viewModel.SelectedOption is null)
        {
            return;
        }

        await OpenCourseEditorAsync(viewModel.SelectedOption.CourseId);
    }

    /// <summary>双击有课格：打开既有课程编辑弹窗（与列表双击行为一致）。</summary>
    [RelayCommand]
    private async Task OpenCellEditorAsync(WeekGridCellItem? cell)
    {
        var course = cell?.SingleEntry;
        if (course is null)
        {
            if (cell is not null && cell.HasConflict)
            {
                await SelectCellCourseAsync(cell);
            }

            return;
        }

        await OpenCourseEditorAsync(course.CourseId);
    }

    /// <summary>按课程 Id 取课程并打开既有课程编辑弹窗；保存过则刷新课表。</summary>
    private async Task OpenCourseEditorAsync(int courseId)
    {
        if (courseId <= 0)
        {
            return;
        }

        var courses = await _courseAdminRepository.GetAllCoursesAsync();
        foreach (var course in courses)
        {
            if (course.Id == courseId)
            {
                if (_dialogService.ShowCourseEditor(course))
                {
                    await LoadScheduleAsync(SelectedStudent);
                }

                return;
            }
        }
    }

    private void OnPeriodTimesChanged(object? sender, EventArgs e)
    {
        RefreshPeriodHeader();
    }

    /// <summary>「定位今天」的结果提示（未配置起始日时给出引导）。</summary>
    [ObservableProperty]
    private string? _locateMessage;

    /// <summary>一键定位：跳到今天所在周（今天列在网格中已高亮）。</summary>
    [RelayCommand]
    private void GoToToday()
    {
        var semester = _semester;
        if (string.IsNullOrWhiteSpace(semester))
        {
            LocateMessage = "还没有可用学期，无法定位今天。";
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var week = _semesterService.GetWeekOf(semester, today);
        if (week is null)
        {
            LocateMessage = $"未配置学期起始日，无法定位今天（{today:M/d}）；请在「设置 → 学期设置」里填第 1 周周一。";
            return;
        }

        SelectedWeek = week.Value;
        WeekHint = $"{semester} 第 {week.Value} 周：今天 {today:M/d}（{ScheduleAdjustmentTypes.GetWeekdayText(DayOfWeekNumber(today))}）";
        LocateMessage = $"已定位到今天：第 {week.Value} 周 {today:M/d}。";
    }

    /// <summary>日历日期转 1–7（1=周一）。</summary>
    private static int DayOfWeekNumber(DateOnly date)
    {
        return date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
    }

    /// <summary>刷新课表条目的节次时刻文本（列表视图节次列使用；页顶不再显示作息图例）。</summary>
    private void RefreshPeriodHeader()
    {
        foreach (var item in _allScheduleItems)
        {
            item.PeriodTimeText = _periodTimeService.GetRangeText(item.StartPeriod, item.EndPeriod);
        }

        ApplyWeekFilter();
    }

    // ── 周次选择（追加）──────────────────────────────────────────

    /// <summary>可选周次（1 ~ 该学期总周数）。</summary>
    [ObservableProperty]
    private ObservableCollection<int> _weekOptions = new();

    /// <summary>当前周次。</summary>
    [ObservableProperty]
    private int _selectedWeek = 1;

    /// <summary>周次提示：第几周的日期区间，或"未配置起始日"的引导。</summary>
    [ObservableProperty]
    private string? _weekHint;

    /// <summary>每天日期表头（周一→周日，含 M/D 与今天标记）。</summary>
    [ObservableProperty]
    private ObservableCollection<DayHeaderItem> _dayHeaders = new();

    /// <summary>学生列表。</summary>
    [ObservableProperty]
    private ObservableCollection<Student> _students = new();

    /// <summary>当前选中的学生，变化时触发课表加载。</summary>
    [ObservableProperty]
    private Student? _selectedStudent;

    /// <summary>当前选中学生的课表。</summary>
    [ObservableProperty]
    private ObservableCollection<StudentScheduleItem> _scheduleItems = new();

    /// <summary>进入界面时加载学生列表、学期与周次。</summary>
    public async Task InitializeAsync()
    {
        var students = await _studentRepository.GetAllStudentsAsync();
        Students = new ObservableCollection<Student>(students);

        await InitializeWeeksAsync();
        await InitializePeriodTimesAsync();
    }

    /// <summary>载入作息时间并刷新行头图例（未配置时降级只显示节次号）。</summary>
    private async Task InitializePeriodTimesAsync()
    {
        try
        {
            await _periodTimeService.LoadAsync();
        }
        catch (Exception ex)
        {
            LocateMessage = $"读取作息时间失败：{ex.Message}";
            return;
        }

        RefreshPeriodHeader();
    }

    /// <summary>载入学期与学期设置，确定周次选项与默认周次（起始日已配置则定位今天所在周）。</summary>
    private async Task InitializeWeeksAsync()
    {
        try
        {
            await _semesterService.LoadAsync();
            var semesters = await _semesterService.GetSemestersAsync();
            _semester = semesters.Count > 0 ? semesters[0] : null;
        }
        catch (Exception ex)
        {
            WeekHint = $"读取学期设置失败：{ex.Message}";
            _semester = null;
        }

        var weekCount = _semester is null
            ? _semesterService.DefaultWeekCount
            : _semesterService.GetWeekCount(_semester);

        var options = new List<int>();
        for (var week = 1; week <= weekCount; week++)
        {
            options.Add(week);
        }

        WeekOptions = new ObservableCollection<int>(options);

        // 起始日已配置 → 默认定位今天所在周；未配置 → 第 1 周
        var todayWeek = _semester is null
            ? null
            : _semesterService.GetWeekOf(_semester, DateOnly.FromDateTime(DateTime.Today));

        SelectedWeek = todayWeek ?? 1;

        UpdateWeekHint();
        ApplyWeekFilter();

        // 叠加调休补课（起始日未配置时相当于不叠加）
        await RefreshAdjustmentPlansAsync();
    }

    private void OnAdjustmentsChanged(object? sender, EventArgs e)
    {
        _ = RefreshAdjustmentPlansAsync();
    }

    /// <summary>刷新周次提示与每天日期表头。</summary>
    private void UpdateWeekHint()
    {
        if (_semester is null)
        {
            WeekHint = "未配置学期起始日，当前按第 1 周显示；请在设置页的「学期设置」中配置学期起始日。";
            DayHeaders = BuildDayHeaders(null);
            return;
        }

        var dates = _semesterService.GetDatesOfWeek(_semester, SelectedWeek);
        if (dates.Count == 0)
        {
            WeekHint = $"未配置学期起始日，当前按第 {SelectedWeek} 周显示；请在设置页的「学期设置」中配置学期起始日。";
            DayHeaders = BuildDayHeaders(null);
            return;
        }

        WeekHint = $"{_semester} 第 {SelectedWeek} 周：{dates[0]:M/d} ~ {dates[6]:M/d}";
        DayHeaders = BuildDayHeaders(dates);
    }

    private static ObservableCollection<DayHeaderItem> BuildDayHeaders(IReadOnlyList<DateOnly>? dates)
    {
        var headers = new List<DayHeaderItem>();
        for (var day = 1; day <= 7; day++)
        {
            var date = dates is not null && dates.Count >= day ? dates[day - 1] : (DateOnly?)null;
            headers.Add(new DayHeaderItem(day, date));
        }

        return new ObservableCollection<DayHeaderItem>(headers);
    }

    /// <summary>只显示 StartWeek ≤ 当前周 ≤ EndWeek 的时段。</summary>
    private void ApplyWeekFilter()
    {
        var filtered = new List<StudentScheduleItem>();
        foreach (var item in _allScheduleItems)
        {
            // 周次范围 + 单双周：统一走 WeekParityTypes.IsActiveInWeek（全项目唯一谓词）
            if (WeekParityTypes.IsActiveInWeek(item.StartWeek, item.EndWeek, item.WeekParity, SelectedWeek))
            {
                filtered.Add(item);
            }
        }

        ScheduleItems = new ObservableCollection<StudentScheduleItem>(filtered);

        // 网格与列表同源：周次/学生/作息变化后一起刷新
        RefreshWeekGrid();
    }

    partial void OnSelectedWeekChanged(int value)
    {
        UpdateWeekHint();
        ApplyWeekFilter();

        // 周次变了要换一套调整计划（放假/调休按日历日换算）
        _ = RefreshAdjustmentPlansAsync();
    }

    /// <summary>选中学生变化时刷新课表。</summary>
    partial void OnSelectedStudentChanged(Student? value)
    {
        // 不阻塞 UI 线程；快速连续切换时由 LoadScheduleAsync 内部丢弃过期结果
        _ = LoadScheduleAsync(value);

        EditStudentCommand.NotifyCanExecuteChanged();
        DeleteStudentCommand.NotifyCanExecuteChanged();
        EnrollCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadScheduleAsync(Student? student)
    {
        if (student is null)
        {
            _allScheduleItems = new List<StudentScheduleItem>();
            ScheduleItems = new ObservableCollection<StudentScheduleItem>();
            return;
        }

        var items = await _studentRepository.GetStudentScheduleAsync(student.Id);

        // 快速切换学生时，前面发起的查询可能后返回：结果已过期则丢弃，避免右侧显示错位的课表
        if (!ReferenceEquals(SelectedStudent, student))
        {
            return;
        }

        _allScheduleItems = new List<StudentScheduleItem>(items);

        // 再按当前周次过滤（周次选择在模块切走再切回时保留）
        ApplyWeekFilter();
    }

    /// <summary>新增学生：弹出表单，保存成功后刷新列表。</summary>
    [RelayCommand]
    private void AddStudent()
    {
        if (_dialogService.ShowStudentEditor(null))
        {
            _ = ReloadStudentsAsync(null);
        }
    }

    /// <summary>编辑当前选中的学生。</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedStudent))]
    private void EditStudent()
    {
        var student = SelectedStudent;
        if (student is null)
        {
            return;
        }

        if (_dialogService.ShowStudentEditor(student))
        {
            _ = ReloadStudentsAsync(student.Id);
        }
    }

    /// <summary>删除当前选中的学生，其选课记录由数据库外键级联删除。</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedStudent))]
    private async Task DeleteStudentAsync()
    {
        var student = SelectedStudent;
        if (student is null)
        {
            return;
        }

        var confirmed = _dialogService.Confirm(
            $"确定删除学生 {student.Name}（{student.StudentNo}）吗？\n\n将同时删除其选课记录。",
            "删除确认");
        if (!confirmed)
        {
            return;
        }

        var result = await _studentAdminRepository.DeleteStudentAsync(student.Id);
        if (!result.Success)
        {
            _dialogService.ShowError(result.ErrorMessage ?? "删除失败。", "删除失败");
            return;
        }

        await ReloadStudentsAsync(null);
    }

    private bool HasSelectedStudent()
    {
        return SelectedStudent is not null;
    }

    /// <summary>打开课程管理窗口；若课程有变化则刷新当前学生的课表。</summary>
    [RelayCommand]
    private async Task ManageCoursesAsync()
    {
        if (await _dialogService.ShowCourseManagerAsync())
        {
            await LoadScheduleAsync(SelectedStudent);
        }
    }

    /// <summary>为当前选中的学生选课；保存后即时刷新其课表。</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedStudent))]
    private async Task EnrollAsync()
    {
        var student = SelectedStudent;
        if (student is null)
        {
            return;
        }

        if (await _dialogService.ShowEnrollmentAsync(student))
        {
            await LoadScheduleAsync(student);
        }
    }

    /// <summary>重新加载学生列表并尽量保持原选中项；keepStudentId 为 null 时保持当前选中。</summary>
    /// <summary>
    /// 批量删除所选学生：确认框列出四计数（学生 / 选课关联 / 考勤保留 / 综测保留），
    /// 执行前自动备份；删除后考勤与综测记录保留（靠学生名快照），提示含备份路径。
    /// </summary>
    [RelayCommand]
    private async Task DeleteStudentsAsync(IReadOnlyList<Student>? students)
    {
        var targets = students?.ToList() ?? new List<Student>();
        if (targets.Count == 0)
        {
            WeekHint = "请先在学生列表里选中要删除的学生（可按住 Ctrl 或 Shift 多选）。";
            return;
        }

        var ids = new List<int>();
        foreach (var student in targets)
        {
            ids.Add(student.Id);
        }

        var impact = await _studentAdminRepository.GetDeleteRangeImpactAsync(ids);

        var confirm = new ConfirmActionViewModel
        {
            Title = "批量删除所选学生",
            Subject = $"共选中 {targets.Count} 名学生",
        };
        confirm.ImpactLines.Add($"学生 {impact.StudentCount} 人");
        confirm.ImpactLines.Add($"选课关联 {impact.EnrollmentCount} 条（一并解除）");
        confirm.RetainedLines.Add($"考勤记录 {impact.AttendanceCount} 条（保留，历史仍显示学生名）");
        confirm.RetainedLines.Add($"综测记录 {impact.ScoreEntryCount} 条（保留，历史仍显示学生名）");

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
            backupNote = $"备份失败（不影响本次删除）：{ex.Message}";
        }

        var result = await _studentAdminRepository.DeleteRangeAsync(ids);
        if (!result.Success)
        {
            WeekHint = result.ErrorMessage ?? "批量删除学生失败。";
            return;
        }

        await ReloadStudentsAsync(null);
        WeekHint = $"已删除 {impact.StudentCount} 名学生，解除选课关联 {impact.EnrollmentCount} 条；" +
            $"考勤 {impact.AttendanceCount} 条与综测 {impact.ScoreEntryCount} 条保留（历史仍显示学生名）。{backupNote}";
    }

    /// <summary>
    /// 批量导入学生：支持 .xlsx / .csv（CSV 自动探测 UTF-8 / GBK），以**学号**为身份键做纯增量导入——
    /// 已存在的学号一律跳过，不覆盖、不修改既有学生；缺学号或缺姓名的行跳过并计入报告。
    /// 导入后刷新学生列表；摘要显示在提示区，完整报告写入 data/import-reports。
    /// </summary>
    [RelayCommand]
    private async Task ImportStudentsAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = _studentImportService.FileFilter,
            Title = "批量导入学生",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var report = await _studentImportService.ImportAsync(dialog.FileName);

        string reportPath;
        try
        {
            var directory = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Data.DatabasePathProvider.GetDatabaseFilePath())!,
                "import-reports");
            System.IO.Directory.CreateDirectory(directory);
            reportPath = System.IO.Path.Combine(directory, $"学生导入_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            System.IO.File.WriteAllText(reportPath, report.ToText());
        }
        catch (Exception ex)
        {
            reportPath = "报告写出失败：" + ex.Message;
        }

        await ReloadStudentsAsync(SelectedStudent?.Id);

        WeekHint = $"学生导入完成：新增 {report.AddedCount} 人，跳过 {report.SkippedCount} 行，失败 {report.FailedCount} 行。" +
            $"完整报告（逐行原因）：{reportPath}";
    }

    /// <summary>
    /// 清空该生课表：只解除该生在本学期的全部课程关联；课程实体与其时段保留（含因此归零的课程），
    /// 考勤记录不受影响。确认框列明影响面后执行，完成后即时刷新网格与列表。
    /// </summary>
    [RelayCommand]
    private async Task ClearStudentScheduleAsync()
    {
        var student = SelectedStudent;
        if (student is null)
        {
            return;
        }

        var courseCount = _allScheduleItems.Select(item => item.CourseId).Distinct().Count();

        var confirm = new ConfirmActionViewModel
        {
            Title = "清空该生课表",
            Subject = $"{student.Name}（{student.StudentNo}）",
        };
        confirm.ImpactLines.Add($"解除该生在本学期的全部课程关联（当前 {courseCount} 门课、{_allScheduleItems.Count} 个时段）");
        confirm.RetainedLines.Add("课程实体与其时段（含因此归零的课程，仅标注，不删除）");
        confirm.RetainedLines.Add("考勤记录（不受影响）");

        if (_dialogService.ShowDialog(confirm) != true)
        {
            return;
        }

        var result = await _courseAdminRepository.ClearStudentScheduleAsync(student.Id, _semester ?? string.Empty);
        if (!result.Success)
        {
            WeekHint = result.ErrorMessage ?? "清空该生课表失败。";
            return;
        }

        await LoadScheduleAsync(student);
        WeekHint = $"已清空 {student.Name} 在本学期的课表；课程实体与考勤记录不受影响。";
    }

    private async Task ReloadStudentsAsync(int? keepStudentId)
    {
        var students = await _studentRepository.GetAllStudentsAsync();
        var targetId = keepStudentId ?? SelectedStudent?.Id;

        Students = new ObservableCollection<Student>(students);

        // 不用 LINQ：查询逻辑只允许出现在 Repositories 层
        Student? reselect = null;
        foreach (var student in Students)
        {
            if (student.Id == targetId)
            {
                reselect = student;
                break;
            }
        }

        SelectedStudent = reselect;
    }
}
