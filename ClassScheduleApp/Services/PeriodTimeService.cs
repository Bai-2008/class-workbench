using System.Globalization;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services;

/// <summary>
/// 作息时间服务实现：内存缓存 + 论坛式降级（无数据返回空文本），保存走仓储并广播刷新。
/// </summary>
public sealed class PeriodTimeService : IPeriodTimeService
{
    private readonly IPeriodTimeRepository _repository;
    private readonly List<PeriodTime> _cache = new();

    public PeriodTimeService(IPeriodTimeRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public int MinPeriod => 1;

    /// <inheritdoc />
    public int MaxPeriod => 12;

    /// <inheritdoc />
    public int PeriodCount => _cache.Count > 0 ? _cache.Count : 12;

    /// <inheritdoc />
    public int MinPeriodCount => 4;

    /// <inheritdoc />
    public int MaxPeriodCount => 24;

    /// <inheritdoc />
    public async Task<OperationResult> SetPeriodCountAsync(
        int periodCount,
        CancellationToken cancellationToken = default)
    {
        if (periodCount < MinPeriodCount || periodCount > MaxPeriodCount)
        {
            return OperationResult.Fail($"节数需在 {MinPeriodCount}–{MaxPeriodCount} 之间。");
        }

        var existing = new Dictionary<int, PeriodTime>();
        foreach (var item in _cache)
        {
            existing[item.PeriodNo] = item;
        }

        var items = new List<PeriodTime>();
        var start = new TimeOnly(8, 30);
        for (var period = 1; period <= periodCount; period++)
        {
            if (existing.TryGetValue(period, out var kept))
            {
                items.Add(new PeriodTime { PeriodNo = period, StartTime = kept.StartTime, EndTime = kept.EndTime });
                start = kept.EndTime;
            }
            else
            {
                // 新增节次：接在上一节之后，默认 45 分钟一节，间隔 5 分钟
                var begin = start.AddMinutes(5);
                items.Add(new PeriodTime { PeriodNo = period, StartTime = begin, EndTime = begin.AddMinutes(45) });
                start = begin.AddMinutes(45);
            }
        }

        return await SaveAsync(items, cancellationToken);
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);

        _cache.Clear();
        _cache.AddRange(items);
        _cache.Sort(static (left, right) => left.PeriodNo.CompareTo(right.PeriodNo));
    }

    /// <inheritdoc />
    public IReadOnlyList<PeriodTime> GetAll()
    {
        return _cache;
    }

    /// <inheritdoc />
    public string GetPeriodRangeText(int periodNo)
    {
        foreach (var item in _cache)
        {
            if (item.PeriodNo == periodNo)
            {
                return $"{Format(item.StartTime)}-{Format(item.EndTime)}";
            }
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public string GetRangeText(int startPeriod, int endPeriod)
    {
        TimeOnly? start = null;
        TimeOnly? end = null;

        foreach (var item in _cache)
        {
            if (item.PeriodNo == startPeriod)
            {
                start = item.StartTime;
            }

            if (item.PeriodNo == endPeriod)
            {
                end = item.EndTime;
            }
        }

        return start.HasValue && end.HasValue
            ? $"{Format(start.Value)}-{Format(end.Value)}"
            : string.Empty;
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveAsync(
        IReadOnlyList<PeriodTime> items,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            if (item.PeriodNo < MinPeriod || item.PeriodNo > MaxPeriodCount)
            {
                return OperationResult.Fail($"节次号必须在 {MinPeriod}–{MaxPeriodCount} 之间。");
            }

            if (item.EndTime <= item.StartTime)
            {
                return OperationResult.Fail($"第 {item.PeriodNo} 节：结束时间必须晚于开始时间。");
            }
        }

        var result = await _repository.UpdateAllAsync(items, cancellationToken);
        if (!result.Success)
        {
            return result;
        }

        _cache.Clear();
        _cache.AddRange(items);
        _cache.Sort(static (left, right) => left.PeriodNo.CompareTo(right.PeriodNo));

        Changed?.Invoke(this, EventArgs.Empty);
        return OperationResult.Ok();
    }

    private static string Format(TimeOnly time)
    {
        return time.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
