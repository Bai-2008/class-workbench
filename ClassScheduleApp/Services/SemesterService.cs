using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services;

/// <summary>
/// 学期服务实现：学期列表复用空闲时间统计的学期来源（IFreeTimeRepository.GetSemestersAsync），
/// 学期设置读写走 ISemesterSettingRepository，并在内存里缓存一份供同步换算使用。
/// </summary>
public sealed class SemesterService : ISemesterService
{
    private readonly IFreeTimeRepository _freeTimeRepository;
    private readonly ISemesterSettingRepository _semesterSettingRepository;
    private readonly Dictionary<string, SemesterSetting> _cache = new(StringComparer.Ordinal);
    private bool _loaded;

    public SemesterService(
        IFreeTimeRepository freeTimeRepository,
        ISemesterSettingRepository semesterSettingRepository)
    {
        _freeTimeRepository = freeTimeRepository;
        _semesterSettingRepository = semesterSettingRepository;
    }

    /// <inheritdoc />
    public int DefaultWeekCount => SemesterSettings.DefaultWeekCount;

    /// <inheritdoc />
    public int MinWeekCount => SemesterSettings.MinWeekCount;

    /// <inheritdoc />
    public int MaxWeekCount => SemesterSettings.MaxWeekCount;

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _semesterSettingRepository.GetAllAsync(cancellationToken);

        _cache.Clear();
        foreach (var setting in settings)
        {
            _cache[setting.SemesterName] = setting;
        }

        _loaded = true;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetSemestersAsync(CancellationToken cancellationToken = default)
    {
        return _freeTimeRepository.GetSemestersAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Repositories.SemesterUsage> GetUsageAsync(string semester, CancellationToken cancellationToken = default)
    {
        return await _semesterSettingRepository.GetUsageAsync(semester, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Repositories.OperationResult> DeleteAsync(
        string semester,
        bool includeScheduleData,
        CancellationToken cancellationToken = default)
    {
        var result = await _semesterSettingRepository.DeleteAsync(semester, includeScheduleData, cancellationToken);
        if (result.Success)
        {
            // 让缓存的起始日/周数立即反映删除后的状态
            await LoadAsync(cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<SemesterSetting?> GetSettingAsync(string semester, CancellationToken cancellationToken = default)
    {
        if (!_loaded)
        {
            await LoadAsync(cancellationToken);
        }

        return _cache.TryGetValue(semester, out var setting) ? setting : null;
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveAsync(
        string semester,
        DateOnly? startDate,
        int weekCount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(semester))
        {
            return OperationResult.Fail("请先选择学期。");
        }

        if (weekCount < SemesterSettings.MinWeekCount || weekCount > SemesterSettings.MaxWeekCount)
        {
            return OperationResult.Fail($"总周数需在 {SemesterSettings.MinWeekCount}–{SemesterSettings.MaxWeekCount} 之间。");
        }

        var result = await _semesterSettingRepository.SaveAsync(semester, startDate, weekCount, cancellationToken);
        if (!result.Success)
        {
            return result;
        }

        _cache[semester] = new SemesterSetting
        {
            SemesterName = semester,
            StartDate = startDate,
            WeekCount = weekCount,
        };
        _loaded = true;

        return OperationResult.Ok();
    }

    /// <inheritdoc />
    public int GetWeekCount(string semester)
    {
        return _cache.TryGetValue(semester, out var setting) ? setting.WeekCount : SemesterSettings.DefaultWeekCount;
    }

    /// <inheritdoc />
    public DateOnly? GetStartDate(string semester)
    {
        return _cache.TryGetValue(semester, out var setting) ? setting.StartDate : null;
    }

    /// <inheritdoc />
    public int? GetWeekOf(string semester, DateOnly date)
    {
        var start = GetStartDate(semester);
        if (start is null)
        {
            return null;
        }

        var days = date.DayNumber - start.Value.DayNumber;
        if (days < 0)
        {
            return null;
        }

        var week = days / 7 + 1;
        return week > GetWeekCount(semester) ? null : week;
    }

    /// <inheritdoc />
    public IReadOnlyList<DateOnly> GetDatesOfWeek(string semester, int week)
    {
        var start = GetStartDate(semester);
        if (start is null || week < SemesterSettings.MinWeekCount)
        {
            return Array.Empty<DateOnly>();
        }

        // 返回顺序固定为 周一→周日（列号 1..7）。起始日不一定是周一时，
        // 直接按偏移取会让整列星期错位一天，因此在本周窗口内对齐到真实周一。
        var weekStart = start.Value.AddDays((week - 1) * 7);
        var weekStartWeekday = weekStart.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)weekStart.DayOfWeek;
        var monday = weekStart.AddDays((((1 - weekStartWeekday) % 7) + 7) % 7);

        var dates = new List<DateOnly>(7);
        for (var offset = 0; offset < 7; offset++)
        {
            dates.Add(monday.AddDays(offset));
        }

        return dates;
    }
}
