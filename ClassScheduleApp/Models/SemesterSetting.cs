namespace ClassScheduleApp.Models;

/// <summary>
/// 学期设置实体，对应 SemesterSettings 表（一表一类）。
/// 学期名与 StudentCourses.Semester 取值一致；起始日是第 1 周的周一，未配置时为空。
/// </summary>
public class SemesterSetting
{
    /// <summary>主键。</summary>
    public int Id { get; set; }

    /// <summary>学期名（唯一），与 StudentCourses.Semester 取值一致。</summary>
    public string SemesterName { get; set; } = string.Empty;

    /// <summary>第 1 周周一；未配置为 null（调用方据此降级，不崩溃）。</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>学期总周数，默认 20。</summary>
    public int WeekCount { get; set; } = SemesterSettings.DefaultWeekCount;
}

/// <summary>
/// 学期设置的公共默认值。
/// </summary>
public static class SemesterSettings
{
    /// <summary>未配置时的默认总周数。</summary>
    public const int DefaultWeekCount = 20;

    /// <summary>总周数的合法范围。</summary>
    public const int MinWeekCount = 1;

    /// <summary>总周数的合法上限。</summary>
    public const int MaxWeekCount = 30;
}
