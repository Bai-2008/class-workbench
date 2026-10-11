using ClassScheduleApp.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 考勤登记页的一名学生：默认出勤，可切换 4 种状态并填写备注。
/// 4 个 bool 属性只用于 RadioButton 的单向显示；状态切换由 4 个命令驱动
/// （WPF 的 RadioButton 点击走 SetCurrentValue，不会回写 IsChecked 的绑定源）。
/// </summary>
public partial class AttendanceRowViewModel : ObservableObject
{
    public AttendanceRowViewModel(int studentId, string studentNo, string name)
    {
        StudentId = studentId;
        StudentNo = studentNo;
        Name = name;
        GroupName = $"attendance_status_{studentId}";
    }

    /// <summary>学生 Id。</summary>
    public int StudentId { get; }

    /// <summary>学号。</summary>
    public string StudentNo { get; }

    /// <summary>姓名。</summary>
    public string Name { get; }

    /// <summary>RadioButton 分组名（按学生唯一，避免跨行互斥）。</summary>
    public string GroupName { get; }

    /// <summary>考勤状态，默认出勤。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPresent))]
    [NotifyPropertyChangedFor(nameof(IsLate))]
    [NotifyPropertyChangedFor(nameof(IsAbsent))]
    [NotifyPropertyChangedFor(nameof(IsLeave))]
    private AttendanceStatus _status = AttendanceStatus.Present;

    /// <summary>备注。</summary>
    [ObservableProperty]
    private string _note = string.Empty;

    /// <summary>是否出勤。</summary>
    public bool IsPresent => Status == AttendanceStatus.Present;

    /// <summary>是否迟到。</summary>
    public bool IsLate => Status == AttendanceStatus.Late;

    /// <summary>是否旷课。</summary>
    public bool IsAbsent => Status == AttendanceStatus.Absent;

    /// <summary>是否请假。</summary>
    public bool IsLeave => Status == AttendanceStatus.Leave;

    /// <summary>切换到出勤。</summary>
    [RelayCommand]
    private void SetPresent()
    {
        Status = AttendanceStatus.Present;
    }

    /// <summary>切换到迟到。</summary>
    [RelayCommand]
    private void SetLate()
    {
        Status = AttendanceStatus.Late;
    }

    /// <summary>切换到旷课。</summary>
    [RelayCommand]
    private void SetAbsent()
    {
        Status = AttendanceStatus.Absent;
    }

    /// <summary>切换到请假。</summary>
    [RelayCommand]
    private void SetLeave()
    {
        Status = AttendanceStatus.Leave;
    }
}
