using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.Services;

/// <summary>
/// 通用弹窗 ViewModel 契约：弹窗壳只认这个接口，不认识任何具体弹窗类型。
/// 放在 Services 层是因为它是「弹窗服务要求的入参形状」——这样 Services 不必反向引用 ViewModels 层，
/// 而 ViewModels 层的 DialogViewModelBase 实现它（依赖方向仍是 ViewModels → Services）。
/// </summary>
public interface IDialogViewModel : INotifyPropertyChanged
{
    /// <summary>弹窗标题。</summary>
    string Title { get; }

    /// <summary>弹窗结果：true = 确定，false = 取消，null = 尚未决定（壳据此关闭窗口）。</summary>
    bool? DialogResult { get; }

    /// <summary>确定按钮命令（校验不通过时 CanExecute 为 false，按钮禁用）。</summary>
    IRelayCommand ConfirmCommand { get; }

    /// <summary>取消按钮命令。</summary>
    IRelayCommand CancelCommand { get; }
}
