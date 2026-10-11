using ClassScheduleApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 弹窗 ViewModel 基类：暴露 Title / DialogResult / ConfirmCommand / CancelCommand，与弹窗壳解耦。
/// 弹窗只负责收集与校验输入，不落库；实际持久化由发起它的页面 ViewModel 完成。
/// </summary>
public abstract partial class DialogViewModelBase : ObservableObject, IDialogViewModel
{
    /// <summary>弹窗标题（由派生类在初始化时按"新增/编辑"设置）。</summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>弹窗结果：true = 确定，false = 取消。</summary>
    [ObservableProperty]
    private bool? _dialogResult;

    /// <summary>确定：校验通过才置结果。</summary>
    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        if (!CanConfirm())
        {
            return;
        }

        OnConfirm();
        DialogResult = true;
    }

    /// <summary>取消。</summary>
    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
    }

    /// <summary>确定按钮是否可用（校验不通过时返回 false，按钮自动禁用）。</summary>
    protected abstract bool CanConfirm();

    /// <summary>确定时的动作，默认什么都不做。</summary>
    protected virtual void OnConfirm()
    {
    }

    /// <summary>输入变化后刷新确定按钮的可用态。</summary>
    protected void RaiseCanConfirmChanged()
    {
        ConfirmCommand.NotifyCanExecuteChanged();
    }
}
