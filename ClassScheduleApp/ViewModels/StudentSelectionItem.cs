using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 弹窗里的一个学生勾选项。
/// 勾选状态用 <see cref="IsSelected"/> 承载，界面上的 CheckBox 只做单向显示、
/// 由 <see cref="ToggleSelectionCommand"/> 驱动（勾选类控件的点击走 SetCurrentValue，不会回写绑定源）。
/// </summary>
public partial class StudentSelectionItem : ObservableObject
{
    private readonly Action? _onSelectionChanged;

    public StudentSelectionItem(int studentId, string studentNo, string name, Action? onSelectionChanged = null)
    {
        StudentId = studentId;
        StudentNo = studentNo;
        Name = name;
        _onSelectionChanged = onSelectionChanged;
    }

    /// <summary>学生 Id。</summary>
    public int StudentId { get; }

    /// <summary>学号。</summary>
    public string StudentNo { get; }

    /// <summary>姓名。</summary>
    public string Name { get; }

    /// <summary>列表显示文本。</summary>
    public string DisplayText => $"{Name}（{StudentNo}）";

    /// <summary>是否勾选。</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>是否可勾选（编辑已有记录时锁定为原学生）。</summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>切换勾选状态。</summary>
    [RelayCommand]
    private void ToggleSelection()
    {
        if (!IsEnabled)
        {
            return;
        }

        IsSelected = !IsSelected;
        _onSelectionChanged?.Invoke();
    }
}
