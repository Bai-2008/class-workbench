using System.Collections.ObjectModel;
using ClassScheduleApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 主界面外壳 ViewModel：只负责导航（模块切换、导航栏内容与页面装配）。
/// 学生课表页的业务已迁至 <see cref="StudentScheduleViewModel"/>，本类不再承载任何页面业务。
/// 服务定位只允许出现在这里（导航层），业务 ViewModel 与 View 一律禁止。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly INavigationService _navigation;
    private readonly IServiceProvider _services;

    /// <summary>按 ViewModelType 缓存已解析的模块 ViewModel（模块切走再切回时状态保留）。</summary>
    private readonly Dictionary<Type, object> _viewModelCache = new();

    public MainViewModel(
        ISettingsService settingsService,
        INavigationService navigation,
        IServiceProvider services,
        SettingsViewModel settings)
    {
        _settingsService = settingsService;
        _navigation = navigation;
        _services = services;
        Settings = settings;

        _navigation.CurrentModuleChanged += OnCurrentModuleChanged;
        _navigation.VisibleModulesChanged += OnVisibleModulesChanged;

        ApplyNavigationState();
    }

    /// <summary>
    /// 设置页 ViewModel。外壳只单向组合它，二者之间不互相调用；
    /// 导航刷新通过 INavigationService 广播，避免 ViewModel 互相引用。
    /// </summary>
    public SettingsViewModel Settings { get; }

    /// <summary>内容区承载的当前页面 ViewModel；MainWindow 直接绑定它，由隐式 DataTemplate 选 View。</summary>
    [ObservableProperty]
    private object? _currentViewModel;

    /// <summary>导航栏显示的模块（不含固定可见的"设置"）。设置变更后整体重新赋值刷新。</summary>
    [ObservableProperty]
    private ObservableCollection<FeatureModule> _visibleModules = new();

    /// <summary>当前显示的模块。</summary>
    [ObservableProperty]
    private FeatureModule? _currentModule;

    /// <summary>固定显示在导航栏底部的"设置"模块。</summary>
    [ObservableProperty]
    private FeatureModule? _settingsModule;

    /// <summary>切换当前模块。</summary>
    [RelayCommand]
    private void SelectModule(FeatureModule? module)
    {
        if (module is not null)
        {
            _navigation.NavigateTo(module);
        }
    }

    private void OnCurrentModuleChanged(object? sender, EventArgs e)
    {
        CurrentModule = _navigation.CurrentModule;
        CurrentViewModel = ResolveViewModel(CurrentModule);
    }

    /// <summary>
    /// 按模块的 ViewModelType 从容器解析页面 ViewModel，并在本 VM 内缓存。
    /// 各模块 ViewModel 在组合根注册为 Singleton，切走再切回时筛选/选择状态保留。
    /// </summary>
    private object? ResolveViewModel(FeatureModule? module)
    {
        if (module is null)
        {
            return null;
        }

        if (_viewModelCache.TryGetValue(module.ViewModelType, out var cached))
        {
            return cached;
        }

        var viewModel = _services.GetRequiredService(module.ViewModelType);
        _viewModelCache[module.ViewModelType] = viewModel;
        return viewModel;
    }

    private void OnVisibleModulesChanged(object? sender, EventArgs e)
    {
        ApplyNavigationState();
    }

    /// <summary>按设置覆盖值重建导航栏；当前模块若被隐藏则自动切到仍可见的模块。</summary>
    private void ApplyNavigationState()
    {
        FeatureModule? alwaysVisible = null;
        var visible = new List<FeatureModule>();

        foreach (var module in FeatureRegistry.Modules)
        {
            if (module.IsAlwaysVisible)
            {
                alwaysVisible = module;
                continue;
            }

            if (_settingsService.GetEffectiveVisibility(module))
            {
                visible.Add(module);
            }
        }

        SettingsModule = alwaysVisible;
        VisibleModules = new ObservableCollection<FeatureModule>(visible);

        var current = _navigation.CurrentModule;
        var currentStillVisible = false;
        foreach (var module in visible)
        {
            if (ReferenceEquals(module, current))
            {
                currentStillVisible = true;
                break;
            }
        }

        if (!currentStillVisible && !ReferenceEquals(current, alwaysVisible))
        {
            if (visible.Count > 0)
            {
                _navigation.NavigateTo(visible[0]);
            }
            else if (alwaysVisible is not null)
            {
                _navigation.NavigateTo(alwaysVisible);
            }
        }
    }
}
