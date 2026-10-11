namespace ClassScheduleApp.Services;

/// <summary>
/// 主题服务：按「深色 / 浅色 / 跟随系统」切换应用配色。
/// 跟随系统时读取 Windows 个性化设置（AppsUseLightTheme），并在系统主题变化时自动跟随。
/// </summary>
public interface IThemeService
{
    /// <summary>当前模式（Light / Dark / System）。</summary>
    string CurrentMode { get; }

    /// <summary>当前实际生效的是否深色。</summary>
    bool IsDarkEffective { get; }

    /// <summary>切换模式并立即应用（System 时按系统设置解析）。</summary>
    void Apply(string mode);

    /// <summary>按当前模式重新应用一次（系统主题变化时使用）。</summary>
    void Reapply();
}
