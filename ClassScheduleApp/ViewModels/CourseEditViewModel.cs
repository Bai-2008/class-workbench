using System.Collections.ObjectModel;
using System.Globalization;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using ClassScheduleApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 课程新增/编辑窗口的 ViewModel：课程字段（编号、学分为选填）+ 多行上课时段。
/// 保存时按传入的时段清单整体替换该课程的全部时段（支持一周多次）。
/// </summary>
public partial class CourseEditViewModel : ObservableObject
{
    private readonly ICourseAdminRepository _repository;
    private readonly IDialogService _dialogService;

    /// <summary>0 表示新增，非 0 表示编辑该课程。</summary>
    private int _courseId;

    public CourseEditViewModel(ICourseAdminRepository repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
    }

    /// <summary>请求关闭窗口，参数为对话框结果。</summary>
    public event EventHandler<bool>? CloseRequested;

    [ObservableProperty]
    private string _title = "新增课程";

    /// <summary>课程编号（选填，唯一）。</summary>
    [ObservableProperty]
    private string _courseNo = string.Empty;

    [ObservableProperty]
    private string _courseName = string.Empty;

    [ObservableProperty]
    private string _teacher = string.Empty;

    /// <summary>学分（选填）用文本承载，保存时统一解析并校验。</summary>
    [ObservableProperty]
    private string _creditText = string.Empty;

    /// <summary>上课时段（每行一个，可增删；支持一周多次）。</summary>
    [ObservableProperty]
    private ObservableCollection<CourseScheduleRowViewModel> _schedules = new();

    /// <summary>按传入的课程填充表单；传 null 表示新增。</summary>
    public void Initialize(CourseDetail? course)
    {
        if (course is null)
        {
            _courseId = 0;
            Title = "新增课程";
            CourseNo = string.Empty;
            CourseName = string.Empty;
            Teacher = string.Empty;
            CreditText = string.Empty;

            // 新增时给一行默认时段，方便直接录入
            Schedules = new ObservableCollection<CourseScheduleRowViewModel> { new() };
            return;
        }

        _courseId = course.Id;
        Title = $"编辑课程 - {course.CourseName}";
        CourseNo = course.CourseNo ?? string.Empty;
        CourseName = course.CourseName;
        Teacher = course.Teacher;
        CreditText = course.Credit?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;

        var rows = new List<CourseScheduleRowViewModel>();
        foreach (var schedule in course.Schedules)
        {
            rows.Add(new CourseScheduleRowViewModel
            {
                Id = schedule.Id,
                DayIndex = schedule.DayOfWeek is >= 1 and <= 7 ? schedule.DayOfWeek - 1 : 0,
                StartPeriod = schedule.StartPeriod is >= 1 and <= 12 ? schedule.StartPeriod : 1,
                EndPeriod = schedule.EndPeriod is >= 1 and <= 12 ? schedule.EndPeriod : 2,
                StartWeek = schedule.StartWeek is >= 1 and <= 25 ? schedule.StartWeek : 1,
                EndWeek = schedule.EndWeek is >= 1 and <= 25 ? schedule.EndWeek : 16,
                SelectedParity = WeekParityTypes.ToDisplay(schedule.WeekParity),
                Location = schedule.Location,
            });
        }

        Schedules = new ObservableCollection<CourseScheduleRowViewModel>(rows);
    }

    /// <summary>添加一行时段。</summary>
    [RelayCommand]
    private void AddSchedule()
    {
        Schedules.Add(new CourseScheduleRowViewModel());
    }

    /// <summary>删除一行时段。</summary>
    [RelayCommand]
    private void RemoveSchedule(CourseScheduleRowViewModel? row)
    {
        if (row is not null)
        {
            Schedules.Remove(row);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var courseNo = CourseNo.Trim();
        var courseName = CourseName.Trim();
        var teacher = Teacher.Trim();

        if (courseName.Length == 0)
        {
            _dialogService.ShowError("课程名不能为空。", "校验失败");
            return;
        }

        if (teacher.Length == 0)
        {
            _dialogService.ShowError("任课老师不能为空。", "校验失败");
            return;
        }

        // 学分选填：留空存 NULL；填了就必须是大于 0 的数字
        double? credit = null;
        var creditText = CreditText.Trim();
        if (creditText.Length > 0)
        {
            if (!double.TryParse(creditText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
            {
                _dialogService.ShowError("学分留空表示不填；若要填写必须是大于 0 的数字，例如 3 或 3.5。", "校验失败");
                return;
            }

            credit = parsed;
        }

        // 逐行校验时段
        var rows = new List<CourseScheduleDetail>();
        var index = 0;
        foreach (var row in Schedules)
        {
            index++;
            if (row.EndPeriod < row.StartPeriod)
            {
                _dialogService.ShowError($"第 {index} 行：结束节次不能小于起始节次。", "校验失败");
                return;
            }

            if (row.EndWeek < row.StartWeek)
            {
                _dialogService.ShowError($"第 {index} 行：结束周不能小于起始周。", "校验失败");
                return;
            }

            rows.Add(new CourseScheduleDetail
            {
                Id = row.Id,
                DayOfWeek = row.DayIndex + 1,
                StartPeriod = row.StartPeriod,
                EndPeriod = row.EndPeriod,
                StartWeek = row.StartWeek,
                EndWeek = row.EndWeek,
                WeekParity = WeekParityTypes.FromDisplay(row.SelectedParity),
                Location = row.Location.Trim(),
            });
        }

        // 编号选填：填了才查重
        var excludeId = _courseId == 0 ? (int?)null : _courseId;
        if (courseNo.Length > 0 && await _repository.IsCourseNoTakenAsync(courseNo, excludeId))
        {
            _dialogService.ShowError($"课程编号 {courseNo} 已存在，请更换后重试。", "课程编号重复");
            return;
        }

        var course = new CourseDetail
        {
            Id = _courseId,
            CourseNo = courseNo.Length == 0 ? null : courseNo,
            CourseName = courseName,
            Teacher = teacher,
            Credit = credit,
            Schedules = rows,
        };

        var result = _courseId == 0
            ? await _repository.AddCourseCheckedAsync(course)
            : await _repository.UpdateCourseAsync(course);

        if (!result.Success)
        {
            _dialogService.ShowError(result.ErrorMessage ?? "保存失败。", "保存失败");
            return;
        }

        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, false);
    }
}
