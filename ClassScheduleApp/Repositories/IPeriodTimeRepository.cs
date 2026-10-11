using ClassScheduleApp.Models;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 作息时间的数据访问接口：按节次号读取、整体保存 12 节时刻。
/// </summary>
public interface IPeriodTimeRepository
{
    /// <summary>获取全部作息时间，按节次号升序。</summary>
    Task<IReadOnlyList<PeriodTime>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>整体保存（事务内按节次号更新；缺失的节次补插）。</summary>
    Task<OperationResult> UpdateAllAsync(
        IReadOnlyList<PeriodTime> items,
        CancellationToken cancellationToken = default);
}
