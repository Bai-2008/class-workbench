namespace ClassScheduleApp.Services;

/// <summary>
/// 主题模式常量与展示文本（深色 / 浅色 / 跟随系统）。
/// </summary>
public static class AppThemeModes
{
    /// <summary>浅色。</summary>
    public const string Light = "Light";

    /// <summary>深色。</summary>
    public const string Dark = "Dark";

    /// <summary>跟随系统。</summary>
    public const string System = "System";

    /// <summary>下拉显示顺序（默认跟随系统）。</summary>
    public static IReadOnlyList<string> All { get; } = new[] { "跟随系统", "浅色", "深色" };

    /// <summary>展示文本 → 存储值。</summary>
    public static string ToMode(string displayText)
    {
        return displayText switch
        {
            "深色" => Dark,
            "浅色" => Light,
            _ => System,
        };
    }

    /// <summary>存储值 → 展示文本。</summary>
    public static string ToDisplay(string? mode)
    {
        return mode switch
        {
            Dark => "深色",
            Light => "浅色",
            _ => "跟随系统",
        };
    }

    /// <summary>规范化存储值（未知值一律按跟随系统处理）。</summary>
    public static string Normalize(string? mode)
    {
        return mode switch
        {
            Dark => Dark,
            Light => Light,
            _ => System,
        };
    }
}
