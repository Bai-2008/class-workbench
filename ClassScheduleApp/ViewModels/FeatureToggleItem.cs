using ClassScheduleApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 设置页里的一个模块开关。开关切换后立即抛事件，由 SettingsViewModel 落盘并通知导航刷新。
/// </summary>
public partial class FeatureToggleItem : ObservableObject
{
    public FeatureToggleItem(FeatureModule module, bool isVisible)
    {
        Module = module;
        DisplayName = module.DisplayName;
        _isVisible = isVisible;
    }

    /// <summary>可见性发生变化（用户拨动开关）。</summary>
    public event EventHandler? VisibilityChanged;

    /// <summary>对应的模块描述符。</summary>
    public FeatureModule Module { get; }

    /// <summary>显示名称。</summary>
    public string DisplayName { get; }

    /// <summary>是否可见。</summary>
    [ObservableProperty]
    private bool _isVisible;

    partial void OnIsVisibleChanged(bool value)
    {
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
    }
}
