using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using ClassScheduleApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 学生新增/编辑窗口的 ViewModel。
/// 只做状态与校验编排，数据访问全部走仓储；关闭窗口通过 CloseRequested 事件通知 View。
/// </summary>
public partial class StudentEditViewModel : ObservableObject
{
    private readonly IStudentAdminRepository _repository;
    private readonly IDialogService _dialogService;

    /// <summary>0 表示新增，非 0 表示编辑该学生。</summary>
    private int _studentId;

    public StudentEditViewModel(IStudentAdminRepository repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
    }

    /// <summary>请求关闭窗口，参数为对话框结果。</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>性别可选项。</summary>
    public IReadOnlyList<string> GenderOptions { get; } = new[] { "男", "女" };

    [ObservableProperty]
    private string _title = "新增学生";

    [ObservableProperty]
    private string _studentNo = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _gender = "男";

    [ObservableProperty]
    private string _className = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _note = string.Empty;

    /// <summary>按传入的学生填充表单；传 null 表示新增。</summary>
    public void Initialize(Student? student)
    {
        if (student is null)
        {
            Title = "新增学生";
            return;
        }

        _studentId = student.Id;
        Title = $"编辑学生 - {student.Name}";
        StudentNo = student.StudentNo;
        Name = student.Name;
        Gender = student.Gender;
        ClassName = student.ClassName;
        Phone = student.Phone ?? string.Empty;
        Note = student.Note ?? string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var studentNo = StudentNo.Trim();
        var name = Name.Trim();
        var className = ClassName.Trim();

        if (studentNo.Length == 0)
        {
            _dialogService.ShowError("学号不能为空。", "校验失败");
            return;
        }

        if (name.Length == 0)
        {
            _dialogService.ShowError("姓名不能为空。", "校验失败");
            return;
        }

        if (className.Length == 0)
        {
            _dialogService.ShowError("班级不能为空。", "校验失败");
            return;
        }

        var excludeId = _studentId == 0 ? (int?)null : _studentId;
        if (await _repository.IsStudentNoTakenAsync(studentNo, excludeId))
        {
            _dialogService.ShowError($"学号 {studentNo} 已存在，请更换后重试。", "学号重复");
            return;
        }

        var student = new Student
        {
            Id = _studentId,
            StudentNo = studentNo,
            Name = name,
            Gender = Gender,
            ClassName = className,
            Phone = EmptyToNull(Phone),
            Note = EmptyToNull(Note),
        };

        var result = _studentId == 0
            ? await _repository.AddStudentAsync(student)
            : await _repository.UpdateStudentAsync(student);

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

    private static string? EmptyToNull(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
