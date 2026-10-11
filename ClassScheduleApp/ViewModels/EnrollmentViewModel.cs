using System.Collections.ObjectModel;
using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using ClassScheduleApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 选课窗口的 ViewModel：课程复选列表（已选预勾选）+ 按差异保存。
/// </summary>
public partial class EnrollmentViewModel : ObservableObject
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly ICourseAdminRepository _courseRepository;
    private readonly IDialogService _dialogService;
    private readonly IFreeTimeRepository _freeTimeRepository;

    private Student? _student;

    /// <summary>本次选课所属学期（取库内现有学期中最新者；库内尚无学期时回退到种子学期）。</summary>
    private string _semester = Seeding.Semester;

    public EnrollmentViewModel(
        IEnrollmentRepository enrollmentRepository,
        ICourseAdminRepository courseRepository,
        IDialogService dialogService,
        IFreeTimeRepository freeTimeRepository)
    {
        _enrollmentRepository = enrollmentRepository;
        _courseRepository = courseRepository;
        _dialogService = dialogService;
        _freeTimeRepository = freeTimeRepository;
    }

    /// <summary>请求关闭窗口，参数为对话框结果。</summary>
    public event EventHandler<bool>? CloseRequested;

    [ObservableProperty]
    private string _title = "选课";

    [ObservableProperty]
    private string _studentInfo = string.Empty;

    /// <summary>课程复选列表。</summary>
    [ObservableProperty]
    private ObservableCollection<CourseOption> _courseOptions = new();

    /// <summary>加载全部课程与当前已选课程；已选课程预勾选。</summary>
    public async Task InitializeAsync(Student student)
    {
        _student = student;
        Title = $"选课 - {student.Name}";
        StudentInfo = $"{student.Name}（{student.StudentNo}）  {student.ClassName}";

        // 学期取库内现有选课学期中最新者（同一学期同一门课只能选一次）；
        // 不能写死成种子学期，否则导入新课表后选课会存进一个不存在的旧学期。
        var semesters = await _freeTimeRepository.GetSemestersAsync();
        _semester = semesters.Count > 0 ? semesters[0] : Seeding.Semester;

        var courses = await _courseRepository.GetAllCoursesAsync();
        var enrolledCourseIds = await _enrollmentRepository.GetEnrolledCourseIdsAsync(student.Id, _semester);

        var enrolled = new HashSet<int>(enrolledCourseIds);

        var options = new List<CourseOption>();
        foreach (var course in courses)
        {
            options.Add(new CourseOption(course, enrolled.Contains(course.Id)));
        }

        CourseOptions = new ObservableCollection<CourseOption>(options);
    }

    [RelayCommand]
    private void SelectAll()
    {
        SetAllSelected(true);
    }

    [RelayCommand]
    private void SelectNone()
    {
        SetAllSelected(false);
    }

    /// <summary>全选 / 全不选（只看当前列表，界面 CheckBox 单向跟随）。</summary>
    private void SetAllSelected(bool value)
    {
        foreach (var option in CourseOptions)
        {
            option.IsSelected = value;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var student = _student;
        if (student is null)
        {
            CloseRequested?.Invoke(this, false);
            return;
        }

        // 不用 LINQ：查询逻辑只允许出现在 Repositories 层
        var selectedCourseIds = new List<int>();
        foreach (var option in CourseOptions)
        {
            if (option.IsSelected)
            {
                selectedCourseIds.Add(option.CourseId);
            }
        }

        if (selectedCourseIds.Count == 0 &&
            !_dialogService.Confirm("当前没有勾选任何课程，保存后会清空该学生的全部选课记录。确定继续吗？", "确认清空选课"))
        {
            return;
        }

        var result = await _enrollmentRepository.SaveEnrollmentAsync(student.Id, selectedCourseIds, _semester);
        if (!result.Success)
        {
            _dialogService.ShowError(result.ErrorMessage ?? "保存选课失败。", "选课失败");
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
