using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 添加/编辑调整弹窗：类型下拉联动表单，确定时按类型生成一条或多条记录（不落库）。
/// </summary>
public partial class ScheduleAdjustmentEditViewModel : DialogViewModelBase
{
    private readonly string _semester;
    private readonly ScheduleAdjustment? _editing;
    private readonly List<SemesterCourseOption> _courses;

    public ScheduleAdjustmentEditViewModel(
        string semester,
        IReadOnlyList<SemesterCourseOption> courses,
        ScheduleAdjustment? editing = null)
    {
        _semester = semester;
        _editing = editing;
        _courses = new List<SemesterCourseOption>(courses);

        KindOptions = new List<AdjustmentKindOption>
        {
            new(ScheduleAdjustmentTypes.SwapLesson, "调课（停课+补课）"),
            new(ScheduleAdjustmentTypes.Cancelled, "停课"),
            new(ScheduleAdjustmentTypes.MakeUp, "补课"),
            new(ScheduleAdjustmentTypes.Holiday, "放假"),
            new(ScheduleAdjustmentTypes.SwapDay, "调休上课日"),
        };

        var periods = new List<int>();
        for (var period = 1; period <= 12; period++)
        {
            periods.Add(period);
        }

        PeriodOptions = periods;

        var weekdays = new List<KeyValuePair<int, string>>();
        for (var day = 1; day <= 7; day++)
        {
            weekdays.Add(new KeyValuePair<int, string>(day, ScheduleAdjustmentTypes.GetWeekdayText(day)));
        }

        WeekdayOptions = weekdays;

        Title = editing is null ? "添加调整" : "编辑调整";

        if (editing is null)
        {
            _selectedKind = KindOptions[0];
            _affectedDate = DateTime.Today;
            _secondDate = DateTime.Today;
            _selectedCourse = _courses.Count > 0 ? _courses[0] : null;
        }
        else
        {
            _selectedKind = FindKind(editing.AdjustmentType);
            _affectedDate = editing.AffectedDate.ToDateTime(TimeOnly.MinValue);
            _secondDate = _affectedDate;
            _selectedCourse = FindCourse(editing.CourseId);
            _startPeriod = editing.StartPeriod ?? 1;
            _endPeriod = editing.EndPeriod ?? _startPeriod;
            _followWeekday = editing.FollowWeekday ?? 6;
            _newLocation = editing.NewLocation;
            _note = editing.Note;
        }
    }

    /// <summary>类型下拉项。</summary>
    public IReadOnlyList<AdjustmentKindOption> KindOptions { get; }

    /// <summary>节次下拉（1–12）。</summary>
    public IReadOnlyList<int> PeriodOptions { get; }

    /// <summary>星期下拉（1–7）。</summary>
    public IReadOnlyList<KeyValuePair<int, string>> WeekdayOptions { get; }

    /// <summary>课程下拉（该学期可选课程）。</summary>
    public IReadOnlyList<SemesterCourseOption> Courses => _courses;

    /// <summary>当前类型。</summary>
    [ObservableProperty]
    private AdjustmentKindOption _selectedKind;

    /// <summary>课程。</summary>
    [ObservableProperty]
    private SemesterCourseOption? _selectedCourse;

    /// <summary>调整日期（放假 = 起始日期；调课 = 原日期）。</summary>
    [ObservableProperty]
    private DateTime? _affectedDate;

    /// <summary>第二个日期（放假 = 结束日期；调课 = 新日期）。</summary>
    [ObservableProperty]
    private DateTime? _secondDate;

    /// <summary>起始节次。</summary>
    [ObservableProperty]
    private int _startPeriod = 1;

    /// <summary>结束节次。</summary>
    [ObservableProperty]
    private int _endPeriod = 2;

    /// <summary>调课：新时段起始节次。</summary>
    [ObservableProperty]
    private int _newStartPeriod = 1;

    /// <summary>调课：新时段结束节次。</summary>
    [ObservableProperty]
    private int _newEndPeriod = 2;

    /// <summary>调休：该日按周几的课表上课。</summary>
    [ObservableProperty]
    private int _followWeekday = 6;

    /// <summary>补课教室（补课/调课可填）。</summary>
    [ObservableProperty]
    private string? _newLocation;

    /// <summary>备注。</summary>
    [ObservableProperty]
    private string? _note;

    /// <summary>校验提示。</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>是否显示课程下拉。</summary>
    public bool ShowCourse => SelectedKind.Kind is ScheduleAdjustmentTypes.Cancelled
        or ScheduleAdjustmentTypes.MakeUp or ScheduleAdjustmentTypes.SwapLesson;

    /// <summary>是否显示节次起止。</summary>
    public bool ShowPeriod => ShowCourse;

    /// <summary>是否显示「第二个日期」（放假结束日 / 调课新日期）。</summary>
    public bool ShowSecondDate => SelectedKind.Kind is ScheduleAdjustmentTypes.Holiday
        or ScheduleAdjustmentTypes.SwapLesson;

    /// <summary>是否显示新时段节次（调课）。</summary>
    public bool ShowNewPeriod => SelectedKind.Kind == ScheduleAdjustmentTypes.SwapLesson;

    /// <summary>是否显示「上周几的课」。</summary>
    public bool ShowSwapWeekday => SelectedKind.Kind == ScheduleAdjustmentTypes.SwapDay;

    /// <summary>是否显示教室输入。</summary>
    public bool ShowNewLocation => SelectedKind.Kind is ScheduleAdjustmentTypes.MakeUp
        or ScheduleAdjustmentTypes.SwapLesson;

    /// <summary>第一个日期的标签。</summary>
    public string FirstDateLabel => SelectedKind.Kind == ScheduleAdjustmentTypes.Holiday
        ? "放假起始日"
        : SelectedKind.Kind == ScheduleAdjustmentTypes.SwapLesson ? "原课日期" : "调整日期";

    /// <summary>第二个日期的标签。</summary>
    public string SecondDateLabel => SelectedKind.Kind == ScheduleAdjustmentTypes.Holiday ? "放假结束日" : "新课日期";

    partial void OnSelectedKindChanged(AdjustmentKindOption value)
    {
        OnPropertyChanged(nameof(ShowCourse));
        OnPropertyChanged(nameof(ShowPeriod));
        OnPropertyChanged(nameof(ShowSecondDate));
        OnPropertyChanged(nameof(ShowNewPeriod));
        OnPropertyChanged(nameof(ShowSwapWeekday));
        OnPropertyChanged(nameof(ShowNewLocation));
        OnPropertyChanged(nameof(FirstDateLabel));
        OnPropertyChanged(nameof(SecondDateLabel));
        ErrorMessage = null;
        RaiseCanConfirmChanged();
    }

    partial void OnSelectedCourseChanged(SemesterCourseOption? value)
    {
        RaiseCanConfirmChanged();
    }

    partial void OnAffectedDateChanged(DateTime? value)
    {
        RaiseCanConfirmChanged();
    }

    partial void OnSecondDateChanged(DateTime? value)
    {
        RaiseCanConfirmChanged();
    }

    /// <inheritdoc />
    protected override bool CanConfirm()
    {
        var error = Validate();
        ErrorMessage = error;
        return error is null;
    }

    /// <summary>按当前类型生成待保存记录（调课两条、放假按天展开）。</summary>
    public IReadOnlyList<ScheduleAdjustment> Build()
    {
        var items = new List<ScheduleAdjustment>();
        var date = DateOnly.FromDateTime(AffectedDate ?? DateTime.Today);
        var second = DateOnly.FromDateTime(SecondDate ?? AffectedDate ?? DateTime.Today);

        switch (SelectedKind.Kind)
        {
            case ScheduleAdjustmentTypes.Holiday:
                for (var day = date; day <= second; day = day.AddDays(1))
                {
                    items.Add(New(ScheduleAdjustmentTypes.Holiday, null, day, null, null, null, null));
                }

                break;

            case ScheduleAdjustmentTypes.SwapDay:
                items.Add(New(ScheduleAdjustmentTypes.SwapDay, null, date, null, null, null, FollowWeekday));
                break;

            case ScheduleAdjustmentTypes.SwapLesson:
                // 调课 = 停课（原时段）+ 补课（新时段），一次事务写入
                items.Add(New(ScheduleAdjustmentTypes.Cancelled, SelectedCourse?.Id, date, StartPeriod, EndPeriod, null, null));
                items.Add(New(ScheduleAdjustmentTypes.MakeUp, SelectedCourse?.Id, second, NewStartPeriod, NewEndPeriod, NewLocation, null));
                break;

            default:
                items.Add(New(
                    SelectedKind.Kind,
                    SelectedCourse?.Id,
                    date,
                    StartPeriod,
                    EndPeriod,
                    SelectedKind.Kind == ScheduleAdjustmentTypes.MakeUp ? NewLocation : null,
                    null));
                break;
        }

        return items;
    }

    private string? Validate()
    {
        if (AffectedDate is null)
        {
            return "请选择日期。";
        }

        switch (SelectedKind.Kind)
        {
            case ScheduleAdjustmentTypes.Holiday:
                if (SecondDate is null)
                {
                    return "请选择放假结束日。";
                }

                var start = DateOnly.FromDateTime(AffectedDate.Value);
                var end = DateOnly.FromDateTime(SecondDate.Value);
                if (end < start)
                {
                    return "放假结束日不能早于起始日。";
                }

                if (end.DayNumber - start.DayNumber > 90)
                {
                    return "放假区间过长（上限 90 天），请确认后重试。";
                }

                return null;

            case ScheduleAdjustmentTypes.SwapDay:
                return FollowWeekday is >= 1 and <= 7 ? null : "请选择该日按周几的课表上课。";

            case ScheduleAdjustmentTypes.SwapLesson:
                if (SelectedCourse is null)
                {
                    return "请选择课程。";
                }

                if (SecondDate is null)
                {
                    return "请选择新课日期。";
                }

                return ValidatePeriods(StartPeriod, EndPeriod) ?? ValidatePeriods(NewStartPeriod, NewEndPeriod);

            default:
                if (SelectedCourse is null)
                {
                    return "请选择课程。";
                }

                return ValidatePeriods(StartPeriod, EndPeriod);
        }
    }

    private static string? ValidatePeriods(int start, int end)
    {
        if (start < 1 || end > 12)
        {
            return "节次需在 1–12 之间。";
        }

        return end < start ? "结束节次不能早于起始节次。" : null;
    }

    private ScheduleAdjustment New(
        int type,
        int? courseId,
        DateOnly date,
        int? startPeriod,
        int? endPeriod,
        string? newLocation,
        int? followWeekday)
    {
        return new ScheduleAdjustment
        {
            Id = _editing?.Id ?? 0,
            Semester = _semester,
            AdjustmentType = type,
            CourseId = courseId,
            AffectedDate = date,
            StartPeriod = startPeriod,
            EndPeriod = endPeriod,
            FollowWeekday = followWeekday,
            NewLocation = string.IsNullOrWhiteSpace(newLocation) ? null : newLocation.Trim(),
            Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
            CreatedAt = DateTime.Now,
        };
    }

    private AdjustmentKindOption FindKind(int kind)
    {
        foreach (var option in KindOptions)
        {
            if (option.Kind == kind)
            {
                return option;
            }
        }

        return KindOptions[0];
    }

    private SemesterCourseOption? FindCourse(int? courseId)
    {
        if (courseId is null)
        {
            return null;
        }

        foreach (var course in _courses)
        {
            if (course.Id == courseId.Value)
            {
                return course;
            }
        }

        return null;
    }
}
