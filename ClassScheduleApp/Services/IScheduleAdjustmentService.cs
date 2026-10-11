using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services;

/// <summary>
/// 调休补课服务：缓存某学期的调整记录，并把「日期 → 生效内容」换算好给课表页使用。
/// 前置依赖：SemesterSettings 起始日必须已配置，否则 <see cref="IsConfigured"/> 为 false，
/// 本模块整体禁用（调用方据此提示，不做任何叠加）。
/// 日期换算一律走 <see cref="ISemesterService"/>，本服务不重复实现周计算。
/// </summary>
public interface IScheduleAdjustmentService
{
    /// <summary>调整数据变更（新增/修改/删除后触发，课表页与空闲统计据此刷新）。</summary>
    event EventHandler? Changed;

    /// <summary>该学期是否已配置起始日（未配置 → 模块整体禁用）。</summary>
    bool IsConfigured(string? semester);

    /// <summary>载入学期设置与该学期的调整到内存缓存；返回是否可用（起始日已配置）。</summary>
    Task<bool> LoadAsync(string? semester, CancellationToken cancellationToken = default);

    /// <summary>当前缓存的调整（按日期升序）。</summary>
    IReadOnlyList<ScheduleAdjustment> GetCached();

    /// <summary>取该学期调整（实时查库，模块页列表用）。</summary>
    Task<IReadOnlyList<ScheduleAdjustment>> GetBySemesterAsync(string semester, CancellationToken cancellationToken = default);

    /// <summary>取该日生效计划（来自缓存）；学期不可用或未载入时返回 null。</summary>
    AdjustmentDayPlan? GetDayPlan(string? semester, DateOnly date);

    /// <summary>取该周 7 天（周一→周日）的生效计划；学期不可用或起始日缺失时返回空集合。</summary>
    IReadOnlyList<AdjustmentDayPlan> GetWeekPlans(string? semester, int week);

    /// <summary>新增多条调整（事务），随后刷新缓存并广播变更。</summary>
    Task<OperationResult> SaveRangeAsync(
        IReadOnlyList<ScheduleAdjustment> adjustments,
        CancellationToken cancellationToken = default);

    /// <summary>删除一条调整，随后刷新缓存并广播变更。</summary>
    Task<OperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>更新一条调整，随后刷新缓存并广播变更。</summary>
    Task<OperationResult> UpdateAsync(ScheduleAdjustment adjustment, CancellationToken cancellationToken = default);

    /// <summary>取该学期可选课程（弹窗课程下拉）。</summary>
    Task<IReadOnlyList<SemesterCourseOption>> GetCoursesAsync(string semester, CancellationToken cancellationToken = default);
}
