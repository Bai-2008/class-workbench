namespace ClassScheduleApp.Services;

/// <summary>
/// 模块可见性设置服务：读写 data/settings.json 中的用户覆盖值。
/// </summary>
public interface ISettingsService
{
    /// <summary>取模块的实际可见性：优先用户覆盖值，未记录则回退注册表默认值。</summary>
    bool GetEffectiveVisibility(FeatureModule module);

    /// <summary>记录用户对该模块可见性的显式修改，并立即写入磁盘。</summary>
    void SetModuleVisibility(string moduleId, bool isVisible);

    /// <summary>清空全部覆盖记录（删除 settings.json），恢复注册表默认值。</summary>
    void ResetToDefaults();

    /// <summary>取主题模式（AppThemeModes.Light / Dark / System）；未设置过返回跟随系统。</summary>
    string GetThemeMode();

    /// <summary>记录主题模式并立即写入磁盘。</summary>
    void SetThemeMode(string mode);
}
