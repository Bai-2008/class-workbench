using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services;

/// <summary>
/// 作息时间服务：缓存各节起止时刻，供课表页与空闲统计页拼行头文本；
/// 设置页整体保存后触发 <see cref="Changed"/>，各页面据此即时刷新行头。
/// 未载入或缺失节次时返回空文本（调用方降级，不崩溃）。
/// </summary>
public interface IPeriodTimeService
{
    /// <summary>节次号范围：1–12。</summary>
    int MinPeriod { get; }

    /// <summary>节次号范围：1–12。</summary>
    int MaxPeriod { get; }

    /// <summary>当前配置的每天节数（来自作息时间表行数；未载入时为 12）。</summary>
    int PeriodCount { get; }

    /// <summary>节数可设置范围。</summary>
    int MinPeriodCount { get; }

    /// <summary>节数可设置范围。</summary>
    int MaxPeriodCount { get; }

    /// <summary>增删作息行到指定节数（保留已有时刻，新增节次用默认时刻），随后整体保存。</summary>
    Task<OperationResult> SetPeriodCountAsync(int periodCount, CancellationToken cancellationToken = default);

    /// <summary>作息时间已保存（各页面刷新行头）。</summary>
    event EventHandler? Changed;

    /// <summary>从库里载入作息时间到内存缓存。</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>当前缓存（按节次号升序）。</summary>
    IReadOnlyList<PeriodTime> GetAll();

    /// <summary>单节时间文本，如 "10:25-11:10"；无该节返回空串。</summary>
    string GetPeriodRangeText(int periodNo);

    /// <summary>跨节时间文本，如 "10:25-12:00"（取首节起、末节止）；缺数据返回空串。</summary>
    string GetRangeText(int startPeriod, int endPeriod);

    /// <summary>整体保存并刷新缓存，随后触发 <see cref="Changed"/>。</summary>
    Task<OperationResult> SaveAsync(
        IReadOnlyList<PeriodTime> items,
        CancellationToken cancellationToken = default);
}
