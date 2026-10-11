namespace ClassScheduleApp.Services;

/// <summary>
/// 导航服务：承载"当前模块"与"可见模块变化"这两件事，
/// 让 ViewModel 之间无需互相引用即可联动（设置页改开关 → 主界面刷新导航栏）。
/// </summary>
public interface INavigationService
{
    /// <summary>当前显示的模块。</summary>
    FeatureModule? CurrentModule { get; }

    /// <summary>当前模块发生变化。</summary>
    event EventHandler? CurrentModuleChanged;

    /// <summary>可见模块集合发生变化（用户改了开关或恢复默认）。</summary>
    event EventHandler? VisibleModulesChanged;

    /// <summary>切换到指定模块。</summary>
    void NavigateTo(FeatureModule module);

    /// <summary>通知可见模块集合已变化，由设置页在保存后调用。</summary>
    void NotifyVisibleModulesChanged();
}
