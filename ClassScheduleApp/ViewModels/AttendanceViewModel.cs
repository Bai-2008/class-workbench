using System.Collections.ObjectModel;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using ClassScheduleApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 考勤管理页 ViewModel：标签页一"考勤登记"、标签页二"统计"。
/// 只做状态编排，查询全部走仓储；不引用其它 ViewModel，也不碰 System.Windows。
/// </summary>
public partial class AttendanceViewModel : ObservableObject
{
    private readonly IAttendanceRepository _repository;
    private readonly IStudentRepository _studentRepository;
    private readonly ICourseAdminRepository _courseRepository;
    private readonly IAttendanceExportService _exportService;
    private readonly IScheduleDayCalculator _scheduleDayCalculator;
    private readonly IBackupService _backupService;

    /// <summary>当前登记日期所属学期（由共享计算服务按日期解析，名单取数与课程列表同一口径）。</summary>
    private string? _daySemester;

    /// <summary>当前登记页对应的学生行（保存时逐行取状态）。</summary>
    private List<AttendanceRowViewModel> _currentRows = new();

    /// <summary>当前统计结果（导出用）。</summary>
    private List<StudentAttendanceStats> _currentStats = new();

    private bool _suppressReload;

    public AttendanceViewModel(
        IAttendanceRepository repository,
        IStudentRepository studentRepository,
        ICourseAdminRepository courseRepository,
        IAttendanceExportService exportService,
        IScheduleDayCalculator scheduleDayCalculator,
        IBackupService backupService)
    {
        _repository = repository;
        _studentRepository = studentRepository;
        _courseRepository = courseRepository;
        _exportService = exportService;
        _scheduleDayCalculator = scheduleDayCalculator;
        _backupService = backupService;

        // 备份恢复 → 数据库已被整体替换，场合、登记表与统计重新查询
        _backupService.Restored += (_, _) =>
        {
            _ = LoadOccasionsAsync();
            _ = LoadRegistrationAsync();
            _ = LoadStatsAsync();
        };

        _suppressReload = true;
        try
        {
            RegistrationDate = DateTime.Today;
            StatsStart = DateTime.Today;
            StatsEnd = DateTime.Today;
        }
        finally
        {
            _suppressReload = false;
        }

        // 首屏加载放到线程池执行并同步等待，避免在线程上阻塞异步调用造成死锁
        try
        {
            Task.Run(InitializeAsync).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            StatusMessage = $"初始化失败：{ex.Message}";
        }
    }

    // ── 标签页一：考勤登记 ──────────────────────────────────────────

    /// <summary>登记日期，默认今天。</summary>
    [ObservableProperty]
    private DateTime? _registrationDate = DateTime.Today;

    /// <summary>场合下拉：班级活动 + 所有已排课课程。</summary>
    [ObservableProperty]
    private ObservableCollection<AttendanceOccasionItem> _occasions = new();

    /// <summary>当前场合。</summary>
    [ObservableProperty]
    private AttendanceOccasionItem? _selectedOccasion;

    /// <summary>全班学生行。</summary>
    [ObservableProperty]
    private ObservableCollection<AttendanceRowViewModel> _rows = new();

    // ── 标签页二：统计 ─────────────────────────────────────────────

    /// <summary>统计起始日期。</summary>
    [ObservableProperty]
    private DateTime? _statsStart = DateTime.Today;

    /// <summary>统计结束日期。</summary>
    [ObservableProperty]
    private DateTime? _statsEnd = DateTime.Today;

    /// <summary>课程过滤下拉：全部 + 各课程。</summary>
    [ObservableProperty]
    private ObservableCollection<AttendanceCourseFilterItem> _courseFilters = new();

    /// <summary>当前课程过滤项（null = 全部）。</summary>
    [ObservableProperty]
    private AttendanceCourseFilterItem? _selectedCourseFilter;

    /// <summary>按出勤率降序的学生统计。</summary>
    [ObservableProperty]
    private ObservableCollection<StudentAttendanceStats> _studentStats = new();

    /// <summary>操作提示。</summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>「日期 → 课程」口径提示（降级/范围外/清空选择等）。</summary>
    [ObservableProperty]
    private string _dayHint = string.Empty;

    /// <summary>一键把全班设为出勤（仍需点保存）。</summary>
    [RelayCommand]
    private void MarkAllPresent()
    {
        foreach (var row in Rows)
        {
            row.Status = AttendanceStatus.Present;
        }

        StatusMessage = "已把全班设为出勤，记得点「保存」。";
    }

    /// <summary>保存本次登记（事务内 upsert，不产生重复记录）。</summary>
    [RelayCommand]
    private async Task SaveRegistrationAsync()
    {
        var occasion = SelectedOccasion;
        if (occasion is null)
        {
            StatusMessage = "请先选择场合。";
            return;
        }

        var date = DateOnly.FromDateTime(RegistrationDate ?? DateTime.Today);
        var entries = new List<AttendanceEntry>();
        foreach (var row in Rows)
        {
            // StudentId <= 0 是"学生已删除"的历史占位行：只读展示，不参与保存
            if (row.StudentId <= 0)
            {
                continue;
            }

            var note = row.Note.Trim();
            entries.Add(new AttendanceEntry(row.StudentId, row.Status, note.Length == 0 ? null : note));
        }

        if (entries.Count == 0)
        {
            StatusMessage = "没有可保存的学生。";
            return;
        }

        var result = await _repository.UpsertBatchAsync(date, occasion.CourseId, entries);
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "保存失败。";
            return;
        }

        // 回显保存内容的状态分布，便于确认这批数据确实是界面上的状态
        var presentCount = 0;
        var lateCount = 0;
        var absentCount = 0;
        var leaveCount = 0;
        foreach (var entry in entries)
        {
            switch (entry.Status)
            {
                case AttendanceStatus.Present:
                    presentCount++;
                    break;
                case AttendanceStatus.Late:
                    lateCount++;
                    break;
                case AttendanceStatus.Absent:
                    absentCount++;
                    break;
                case AttendanceStatus.Leave:
                    leaveCount++;
                    break;
            }
        }

        var summary = $"出勤{presentCount}/迟到{lateCount}/旷课{absentCount}/请假{leaveCount}";
        await LoadRegistrationAsync();
        await LoadStatsAsync();

        StatusMessage = $"已保存：{date:yyyy-MM-dd} {occasion.DisplayText}，共 {entries.Count} 名学生（{summary}）。";
    }

    /// <summary>导出当前统计区间的考勤报表。</summary>
    [RelayCommand]
    private async Task ExportStatsAsync()
    {
        var (start, end) = GetStatsRange();
        var records = await _repository.GetByRangeAsync(start, end, SelectedCourseFilter?.CourseId);

        StatusMessage = "正在导出…";
        var result = await _exportService.ExportAttendanceAsync(start, end, _currentStats, records);
        StatusMessage = result.Success
            ? $"已导出：{result.FilePath}"
            : result.ErrorMessage ?? "导出失败。";
    }

    partial void OnRegistrationDateChanged(DateTime? value)
    {
        if (!_suppressReload)
        {
            // 日期变了：先按新日期重建课程列表（口径与课表页一致），再载入名单
            _ = LoadDayOccasionsAsync();
        }
    }

    partial void OnSelectedOccasionChanged(AttendanceOccasionItem? value)
    {
        if (!_suppressReload)
        {
            _ = LoadRegistrationAsync();
        }
    }

    partial void OnStatsStartChanged(DateTime? value)
    {
        if (!_suppressReload)
        {
            _ = LoadStatsAsync();
        }
    }

    partial void OnStatsEndChanged(DateTime? value)
    {
        if (!_suppressReload)
        {
            _ = LoadStatsAsync();
        }
    }

    partial void OnSelectedCourseFilterChanged(AttendanceCourseFilterItem? value)
    {
        if (!_suppressReload)
        {
            _ = LoadStatsAsync();
        }
    }

    private async Task InitializeAsync()
    {
        await LoadOccasionsAsync();
        await LoadRegistrationAsync();
        await LoadStatsAsync();
    }

    private async Task LoadOccasionsAsync()
    {
        var courses = await _courseRepository.GetAllCoursesAsync();

        // 统计 tab 的过滤下拉保持既有口径（全部场合 + 所有课程），本页冻结范围不动
        var filters = new List<AttendanceCourseFilterItem>
        {
            new(null, "全部场合"),
        };

        foreach (var course in courses)
        {
            filters.Add(new AttendanceCourseFilterItem(course.Id, $"{course.CourseNo} {course.CourseName}"));
        }

        _suppressReload = true;
        try
        {
            CourseFilters = new ObservableCollection<AttendanceCourseFilterItem>(filters);
            SelectedCourseFilter = CourseFilters.Count > 0 ? CourseFilters[0] : null;
        }
        finally
        {
            _suppressReload = false;
        }

        await LoadDayOccasionsAsync();
    }

    /// <summary>
    /// 按登记日期重建「该日期有效课程」下拉 —— 与课表页同一口径：
    /// 星期匹配 + 教学周（经 ISemesterService）+ 单双周 + 调整叠加（放假 / 停课 / 补课 / 调休），
    /// 全部由共享计算服务给出。班级活动不参与日期过滤，始终可选。
    /// </summary>
    private async Task LoadDayOccasionsAsync()
    {
        var date = DateOnly.FromDateTime(RegistrationDate ?? DateTime.Today);
        var previousCourseId = SelectedOccasion?.CourseId;

        var occasions = new List<AttendanceOccasionItem>
        {
            new(null, "班级活动（非上课场合）"),
        };

        var hint = string.Empty;

        try
        {
            var context = await _scheduleDayCalculator.ResolveDayAsync(date);
            _daySemester = context.Semester;

            if (context.Semester is null)
            {
                hint = context.Hint.Length > 0 ? context.Hint : "该日期不属于任何教学学期，课程列表为空。";
            }
            else
            {
                var slots = await _repository.GetSemesterSlotsAsync(context.Semester);

                // 周次 + 单双周统一经共享计算服务；起始日未配置 → 降级为仅按星期过滤
                var weekFiltered = context.Week is { } week
                    ? slots.Where(s => _scheduleDayCalculator.IsActiveInWeek(s.StartWeek, s.EndWeek, s.WeekParity, week)).ToList()
                    : slots.ToList();

                var plan = await _scheduleDayCalculator.GetDayPlanAsync(context.Semester, date);
                // 列号口径由共享计算服务给出（= 教学周内第几天），与课表网格/空闲统计一致
                var weekday = context.Weekday;

                var makeUps = new List<MakeUpSlot>();
                if (plan is not null)
                {
                    foreach (var makeup in plan.MakeUps)
                    {
                        if (makeup.CourseId is null || makeup.StartPeriod is null || makeup.EndPeriod is null)
                        {
                            continue;
                        }

                        var info = slots.FirstOrDefault(s => s.CourseId == makeup.CourseId.Value);
                        makeUps.Add(new MakeUpSlot
                        {
                            CourseId = makeup.CourseId,
                            CourseName = info?.CourseName ?? $"课程#{makeup.CourseId.Value}",
                            Teacher = info?.Teacher ?? string.Empty,
                            Location = string.IsNullOrWhiteSpace(makeup.NewLocation) ? info?.Location ?? string.Empty : makeup.NewLocation!,
                            StartPeriod = makeup.StartPeriod,
                            EndPeriod = makeup.EndPeriod,
                        });
                    }
                }

                var result = _scheduleDayCalculator.CalculateDay(
                    weekday,
                    plan,
                    weekFiltered.Select(ToDaySlot),
                    makeUps);

                foreach (var group in result.Entries.Where(e => e.CourseId > 0).GroupBy(e => e.CourseId).OrderBy(g => g.Min(e => e.StartPeriod)))
                {
                    var first = group.First();
                    var periods = string.Join("、", group
                        .OrderBy(e => e.StartPeriod)
                        .Select(e => e.StartPeriod == e.EndPeriod ? $"{e.StartPeriod}节" : $"{e.StartPeriod}-{e.EndPeriod}节"));
                    var marked = group.Any(e => e.IsMakeUp) ? "（补课）" : string.Empty;
                    occasions.Add(new AttendanceOccasionItem(group.Key, $"{first.CourseName}｜{periods}{marked}"));
                }

                hint = BuildDayHint(context, occasions.Count);
            }
        }
        catch (Exception ex)
        {
            _daySemester = null;
            hint = $"读取该日期课程失败：{ex.Message}";
        }

        _suppressReload = true;
        try
        {
            Occasions = new ObservableCollection<AttendanceOccasionItem>(occasions);

            // 日期变更后原选课程若不再有课 → 清空选择（回落到班级活动）并提示
            var keep = previousCourseId is null
                ? occasions[0]
                : occasions.FirstOrDefault(o => o.CourseId == previousCourseId);
            SelectedOccasion = keep ?? occasions[0];
            if (previousCourseId is not null && keep is null)
            {
                hint = $"所选课程在 {date:yyyy-MM-dd} 没有安排，已清空选择。" + hint;
            }
        }
        finally
        {
            _suppressReload = false;
        }

        DayHint = hint;
        await LoadRegistrationAsync();
    }

    private static string BuildDayHint(ScheduleDayContext context, int occasionCount)
    {
        if (!context.IsConfigured)
        {
            return context.Hint.Length > 0 ? context.Hint : "该学期未配置起始日，仅按星期过滤。";
        }

        if (context.IsOutsideTeachingWeeks)
        {
            return context.Hint.Length > 0 ? context.Hint : "该日期不在教学周范围内，当天没有课程。";
        }

        return occasionCount <= 1
            ? "该日期（或该教学周）没有课程安排。"
            : $"当天共 {occasionCount - 1} 门课程，已在名称后标注节次。";
    }

    private static DayScheduleSlot ToDaySlot(CourseSlotRow row) => new()
    {
        CourseId = row.CourseId,
        CourseName = row.CourseName,
        Teacher = row.Teacher,
        Location = row.Location,
        DayOfWeek = row.DayOfWeek,
        StartPeriod = row.StartPeriod,
        EndPeriod = row.EndPeriod,
    };

    private async Task LoadRegistrationAsync()
    {
        var occasion = SelectedOccasion;
        if (occasion is null)
        {
            Rows = new ObservableCollection<AttendanceRowViewModel>();
            _currentRows = new List<AttendanceRowViewModel>();
            return;
        }

        var date = DateOnly.FromDateTime(RegistrationDate ?? DateTime.Today);

        try
        {
            // 名单按课程关联：选了具体课程 → 只列该课程在**登记日期所属学期**的关联学生；
            // “班级活动（非上课场合）”不属于任何课程 → 仍列全部学生
            IReadOnlyList<Student> students;
            if (occasion.CourseId is null)
            {
                students = await _studentRepository.GetAllStudentsAsync();
            }
            else
            {
                students = _daySemester is null
                    ? Array.Empty<Student>()
                    : await _repository.GetCourseStudentsAsync(occasion.CourseId.Value, _daySemester);
            }

            var records = await _repository.GetByRangeAsync(date, date);

            // 该场合已有的记录：按学生索引（含 CourseId 为 null 的班级活动）；
            // 学生已删除的历史记录（StudentId 为 null）不参与名单匹配
            var existing = new Dictionary<int, AttendanceRecordInfo>();
            foreach (var record in records)
            {
                if (record.CourseId == occasion.CourseId && record.StudentId is { } recordStudentId)
                {
                    existing[recordStudentId] = record;
                }
            }

            var rows = new List<AttendanceRowViewModel>();
            foreach (var student in students)
            {
                var row = new AttendanceRowViewModel(student.Id, student.StudentNo, student.Name);
                if (existing.TryGetValue(student.Id, out var record))
                {
                    row.Status = record.Status;
                    row.Note = record.Note ?? string.Empty;
                }

                rows.Add(row);
            }

            // 历史考勤照常显示：已记录但当前已无课程关联的学生也要出现在名单里
            foreach (var record in existing.Values)
            {
                if (rows.Any(r => r.StudentId == record.StudentId))
                {
                    continue;
                }

                // 学生已删除：行内 StudentId 用 0 占位（保存时跳过，历史只读展示）
                rows.Add(new AttendanceRowViewModel(record.StudentId ?? 0, record.StudentNo, record.StudentName)
                {
                    Status = record.Status,
                    Note = record.Note ?? string.Empty,
                });
            }

            _currentRows = rows;
            Rows = new ObservableCollection<AttendanceRowViewModel>(rows);

            if (rows.Count == 0 && occasion.CourseId is not null)
            {
                StatusMessage = $"{date:yyyy-MM-dd} {occasion.DisplayText}：该课程暂无关联学生，请先录入或导入课表。";
            }
            else
            {
                StatusMessage = $"{date:yyyy-MM-dd} {occasion.DisplayText}：共 {rows.Count} 名学生，已回显 {existing.Count} 条既有记录。";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载考勤登记失败：{ex.Message}";
        }
    }

    private async Task LoadStatsAsync()
    {
        var (start, end) = GetStatsRange();

        try
        {
            var stats = await _repository.GetStudentStatsAsync(start, end, SelectedCourseFilter?.CourseId);
            var list = new List<StudentAttendanceStats>(stats);

            // 按出勤率降序；相同则按学号，保证顺序稳定
            list.Sort(static (left, right) =>
            {
                var byRate = right.AttendanceRate.CompareTo(left.AttendanceRate);
                return byRate != 0 ? byRate : string.CompareOrdinal(left.StudentNo, right.StudentNo);
            });

            _currentStats = list;
            StudentStats = new ObservableCollection<StudentAttendanceStats>(list);
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载统计失败：{ex.Message}";
        }
    }

    private (DateOnly Start, DateOnly End) GetStatsRange()
    {
        var start = DateOnly.FromDateTime(StatsStart ?? DateTime.Today);
        var end = DateOnly.FromDateTime(StatsEnd ?? DateTime.Today);
        return end < start ? (end, start) : (start, end);
    }
}
