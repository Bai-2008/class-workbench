using ClassScheduleApp.Models;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 考勤管理的数据访问接口。
/// Attendance 表没有唯一约束（同一课程同一天可能有多节），是否已记录由
/// <see cref="UpsertBatchAsync"/> 在应用层按 (StudentId, Date, CourseId) 判定。
/// </summary>
public interface IAttendanceRepository
{
    /// <summary>
    /// 取某课程在**指定学期**关联的学生，按姓名排序。
    /// 学期由调用方经共享计算服务按登记日期解析，保证「日期 → 学期 → 名单」同一口径。
    /// </summary>
    Task<IReadOnlyList<Models.Student>> GetCourseStudentsAsync(
        int courseId,
        string semester,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取某学期全部已排课时段（含周次与单双周），供「某日期有效课程」计算。
    /// </summary>
    Task<IReadOnlyList<CourseSlotRow>> GetSemesterSlotsAsync(
        string semester,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询日期区间内的考勤明细；courseId 为 null 表示不按场合筛选。
    /// </summary>
    Task<IReadOnlyList<AttendanceRecordInfo>> GetByRangeAsync(
        DateOnly start,
        DateOnly end,
        int? courseId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量登记：在事务内按 (StudentId, Date, CourseId) 判定，
    /// 已有记录则更新状态与备注，否则插入新记录。
    /// </summary>
    Task<OperationResult> UpsertBatchAsync(
        DateOnly date,
        int? courseId,
        IReadOnlyList<AttendanceEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按学生汇总区间内的考勤次数与出勤率（分母为该生的考核总次数）。
    /// 没有记录的学生也会返回（各项为 0）。courseId 为 null 表示不按场合筛选。
    /// </summary>
    Task<IReadOnlyList<StudentAttendanceStats>> GetStudentStatsAsync(
        DateOnly start,
        DateOnly end,
        int? courseId = null,
        CancellationToken cancellationToken = default);

    /// <summary>按课程/场合汇总区间内的考勤情况。</summary>
    Task<IReadOnlyList<CourseAttendanceStats>> GetCourseStatsAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default);
}
