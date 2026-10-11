namespace ClassScheduleApp.Services;

/// <summary>
/// 功能模块描述符：导航栏上的一项。
/// ViewModelType 由组合根在启动时注入（Services 层不引用 ViewModels 层，保持单向依赖）。
/// </summary>
public sealed class FeatureModule
{
    public FeatureModule(
        string id,
        string displayName,
        string iconPath,
        Type viewModelType,
        bool isVisibleByDefault,
        bool isAlwaysVisible = false)
    {
        Id = id;
        DisplayName = displayName;
        IconPath = iconPath;
        ViewModelType = viewModelType;
        IsVisibleByDefault = isVisibleByDefault;
        IsAlwaysVisible = isAlwaysVisible;
    }

    /// <summary>模块唯一标识，同时作为 settings.json 里的键。</summary>
    public string Id { get; }

    /// <summary>导航栏显示名称。</summary>
    public string DisplayName { get; }

    /// <summary>图标几何路径（Path.Data）。</summary>
    public string IconPath { get; }

    /// <summary>
    /// 模块对应的页面 ViewModel 类型。
    /// 内容区 ContentControl 直接承载该类型的实例，由隐式 DataTemplate（DataType=ViewModel 类型）决定显示哪个 View。
    /// </summary>
    public Type ViewModelType { get; }

    /// <summary>默认是否可见（用户未在 settings.json 中覆盖时使用）。</summary>
    public bool IsVisibleByDefault { get; }

    /// <summary>是否固定可见、不受开关控制，并固定排在导航栏最底部（设置模块）。</summary>
    public bool IsAlwaysVisible { get; }
}
