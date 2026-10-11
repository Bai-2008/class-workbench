using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services;

/// <summary>
/// 学期服务：学期列表、学期设置（起始日 / 总周数）与周次换算。
/// 起始日未配置时，周次相关查询一律降级返回 null / 空集合，调用方据此提示，不崩溃。
/// </summary>
public interface ISemesterService
{
    /// <summary>未配置时的默认总周数。</summary>
    int DefaultWeekCount { get; }

    /// <summary>总周数合法范围下限。</summary>
    int MinWeekCount { get; }

    /// <summary>总周数合法范围上限。</summary>
    int MaxWeekCount { get; }

    /// <summary>把 SemesterSettings 载入内存缓存（界面进入时调用一次）。</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>学期下拉数据：StudentCourses.Semester 去重、倒序（最新在前）。</summary>
    Task<IReadOnlyList<string>> GetSemestersAsync(CancellationToken cancellationToken = default);

    /// <summary>某学期当前被引用的数据量（删除学期前的知情提示）。</summary>
    Task<Repositories.SemesterUsage> GetUsageAsync(string semester, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除某学期：删除学期设置；<paramref name="includeScheduleData"/> 为 true 时一并删除该学期的
    /// 学生选课与调休补课记录。删除后刷新缓存。
    /// </summary>
    Task<Repositories.OperationResult> DeleteAsync(
        string semester,
        bool includeScheduleData,
        CancellationToken cancellationToken = default);

    /// <summary>取某学期的设置；未配置返回 null。</summary>
    Task<SemesterSetting?> GetSettingAsync(string semester, CancellationToken cancellationToken = default);

    /// <summary>保存某学期的起始日与总周数，并刷新缓存。</summary>
    Task<OperationResult> SaveAsync(
        string semester,
        DateOnly? startDate,
        int weekCount,
        CancellationToken cancellationToken = default);

    /// <summary>该学期的总周数；未配置返回默认值。</summary>
    int GetWeekCount(string semester);

    /// <summary>该学期第 1 周周一；未配置返回 null。</summary>
    DateOnly? GetStartDate(string semester);

    /// <summary>某日期属于该学期的第几周；起始日未配置或超出学期范围返回 null。</summary>
    int? GetWeekOf(string semester, DateOnly date);

    /// <summary>该学期某一周的 7 天（周一→周日）；起始日未配置返回空集合。</summary>
    IReadOnlyList<DateOnly> GetDatesOfWeek(string semester, int week);
}
