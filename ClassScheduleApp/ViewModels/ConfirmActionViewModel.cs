using System.Collections.ObjectModel;
using ClassScheduleApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 破坏性操作确认弹窗的 ViewModel（由 DialogShell 承载）：
/// 实现 <see cref="IDialogViewModel"/>——确认/取消置 <see cref="DialogResult"/>，DialogService 监听后关窗。
/// 影响明细与保留明细逐行呈现；高风险操作要求**键入确认词**后方可执行。
/// </summary>
public partial class ConfirmActionViewModel : ObservableObject, IDialogViewModel
{
    /// <summary>弹窗标题，如「删除课程」/「清空所有人的课表」。</summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>操作对象描述，如「测试课（张师）」。</summary>
    [ObservableProperty]
    private string _subject = string.Empty;

    /// <summary>将删除/改动的影响明细行。</summary>
    public ObservableCollection<string> ImpactLines { get; } = new();

    /// <summary>将保留的明细行（如学生、考勤记录）。</summary>
    public ObservableCollection<string> RetainedLines { get; } = new();

    /// <summary>补充说明（如「本操作用于学期清空，完成后需重新导入或录入课表」）。</summary>
    [ObservableProperty]
    private string _notice = string.Empty;

    /// <summary>是否要求键入确认词（大规模清空为 true）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private bool _requireTypedWord;

    /// <summary>要求的确认词，如「清空」。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private string _confirmWord = string.Empty;

    /// <summary>用户键入的确认词。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private string _typedText = string.Empty;

    /// <summary>是否已确认（弹窗结果）。</summary>
    [ObservableProperty]
    private bool _isConfirmed;

    /// <summary>弹窗结果：true=确认执行，false=取消，null=尚未决定（DialogService 据此关窗）。</summary>
    [ObservableProperty]
    private bool? _dialogResult;

    /// <summary>确认按钮是否可用：不需要确认词时恒可用；需要时须逐字匹配（去首尾空白）。</summary>
    public bool CanConfirm => !RequireTypedWord || TypedText.Trim() == ConfirmWord;

    /// <summary>确认词提示文案。</summary>
    public string ConfirmPrompt => RequireTypedWord ? $"请键入「{ConfirmWord}」以确认执行：" : string.Empty;

    /// <summary>确认：仅在 <see cref="CanConfirm"/> 为真时置为已确认并关窗。</summary>
    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (!CanConfirm)
        {
            return;
        }

        IsConfirmed = true;
        DialogResult = true;
    }

    /// <summary>取消：不改动任何数据，仅关窗。</summary>
    [RelayCommand]
    private void Cancel()
    {
        IsConfirmed = false;
        DialogResult = false;
    }

    partial void OnTypedTextChanged(string value) => ConfirmCommand.NotifyCanExecuteChanged();

    partial void OnRequireTypedWordChanged(bool value) => ConfirmCommand.NotifyCanExecuteChanged();

    partial void OnConfirmWordChanged(string value) => ConfirmCommand.NotifyCanExecuteChanged();
}