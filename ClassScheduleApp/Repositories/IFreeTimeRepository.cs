using ClassScheduleApp.Models;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 空闲时间统计的数据访问接口。
/// "有课"判定：DayOfWeek == day 且 StartWeek &lt;= week &lt;= EndWeek
/// 且 StartPeriod &lt;= endPeriod 且 EndPeriod &gt;= startPeriod（区间相交）。
/// 分母固定为 Students 表全部学生；该学期没有选课的学生一律视为空闲。
/// </summary>
public interface IFreeTimeRepository
{
    /// <summary>获取 StudentCourses 中出现过的学期（去重、倒序，"最新"在前）。</summary>
    Task<IReadOnlyList<string>> GetSemestersAsync(CancellationToken cancellationToken = default);

    /// <summary>获取指定时段有课的学生及其课程。</summary>
    Task<IReadOnlyList<BusyStudentInfo>> GetBusyStudentsAsync(
        string semester,
        int week,
        int day,
        int startPeriod,
        int endPeriod,
        CancellationToken cancellationToken = default);

    /// <summary>获取指定时段空闲的学生（全部学生减去有课集合）。</summary>
    Task<IReadOnlyList<Student>> GetFreeStudentsAsync(
        string semester,
        int week,
        int day,
        int startPeriod,
        int endPeriod,
        CancellationToken cancellationToken = default);

    /// <summary>获取 7 天 × 6 个节次对（1-2/3-4/5-6/7-8/9-10/11-12 节）的空闲统计，共 42 格。</summary>
    Task<IReadOnlyList<ClassFreeCell>> GetClassFreeMatrixAsync(
        string semester,
        int week,
        CancellationToken cancellationToken = default);
}
