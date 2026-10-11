using ClassScheduleApp.Models;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 学期设置（SemesterSettings 表）的数据访问接口。
/// 学期名与 StudentCourses.Semester 取值一致；起始日与总周数由用户在设置页维护。
/// </summary>
public interface ISemesterSettingRepository
{
    /// <summary>获取全部学期设置。</summary>
    Task<IReadOnlyList<SemesterSetting>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>获取指定学期的设置；未配置返回 null。</summary>
    Task<SemesterSetting?> GetAsync(string semesterName, CancellationToken cancellationToken = default);

    /// <summary>写入或更新某学期的设置（按学期名 upsert）。</summary>
    Task<OperationResult> SaveAsync(
        string semesterName,
        DateOnly? startDate,
        int weekCount,
        CancellationToken cancellationToken = default);

    /// <summary>某学期当前被引用的数据量（删除前的知情提示）。</summary>
    Task<SemesterUsage> GetUsageAsync(string semesterName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除某学期：删除学期设置；<paramref name="includeScheduleData"/> 为 true 时一并删除该学期的
    /// 学生选课与调休补课记录（一次事务）。为 false 且存在引用数据时返回失败，避免留下"删不掉的学期"。
    /// </summary>
    Task<OperationResult> DeleteAsync(
        string semesterName,
        bool includeScheduleData,
        CancellationToken cancellationToken = default);
}
