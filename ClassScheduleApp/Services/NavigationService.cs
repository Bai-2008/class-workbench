namespace ClassScheduleApp.Services;

/// <summary>
/// 导航服务实现：只保存当前模块并广播事件，不引用任何 ViewModel。
/// </summary>
public sealed class NavigationService : INavigationService
{
    /// <inheritdoc />
    public FeatureModule? CurrentModule { get; private set; }

    /// <inheritdoc />
    public event EventHandler? CurrentModuleChanged;

    /// <inheritdoc />
    public event EventHandler? VisibleModulesChanged;

    /// <inheritdoc />
    public void NavigateTo(FeatureModule module)
    {
        if (ReferenceEquals(CurrentModule, module))
        {
            return;
        }

        CurrentModule = module;
        CurrentModuleChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void NotifyVisibleModulesChanged()
    {
        VisibleModulesChanged?.Invoke(this, EventArgs.Empty);
    }
}
