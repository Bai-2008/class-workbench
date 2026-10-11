using ClassScheduleApp.Models;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 调休补课调整（ScheduleAdjustments 表）的数据访问接口。
/// </summary>
public interface IScheduleAdjustmentRepository
{
    /// <summary>按日期区间取调整（含首含尾，跨学期）。</summary>
    Task<IReadOnlyList<ScheduleAdjustment>> GetByDateRangeAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default);

    /// <summary>取某学期的全部调整（按日期、节次升序）。</summary>
    Task<IReadOnlyList<ScheduleAdjustment>> GetBySemesterAsync(
        string semester,
        CancellationToken cancellationToken = default);

    /// <summary>取某日全部生效调整（按类型、节次升序）。</summary>
    Task<IReadOnlyList<ScheduleAdjustment>> GetEffectiveChangesAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>新增一条调整。</summary>
    Task<OperationResult> AddAsync(ScheduleAdjustment adjustment, CancellationToken cancellationToken = default);

    /// <summary>一次事务新增多条（放假按天展开、调课生成停课+补课）。</summary>
    Task<OperationResult> AddRangeAsync(
        IReadOnlyList<ScheduleAdjustment> adjustments,
        CancellationToken cancellationToken = default);

    /// <summary>更新一条调整。</summary>
    Task<OperationResult> UpdateAsync(ScheduleAdjustment adjustment, CancellationToken cancellationToken = default);

    /// <summary>按 Id 删除一条调整。</summary>
    Task<OperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>取该学期可选课程（经 StudentCourses 关联去重），供弹窗课程下拉。</summary>
    Task<IReadOnlyList<SemesterCourseOption>> GetCoursesBySemesterAsync(
        string semester,
        CancellationToken cancellationToken = default);
}
