using System.Globalization;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services;

/// <summary>
/// 调休补课服务实现：内存缓存该学期调整，按天聚合出「生效计划」。
/// </summary>
public sealed class ScheduleAdjustmentService : IScheduleAdjustmentService
{
    private readonly IScheduleAdjustmentRepository _repository;
    private readonly ISemesterService _semesterService;
    private readonly List<ScheduleAdjustment> _cache = new();
    private string? _cachedSemester;
    private bool _loaded;

    public ScheduleAdjustmentService(
        IScheduleAdjustmentRepository repository,
        ISemesterService semesterService)
    {
        _repository = repository;
        _semesterService = semesterService;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public bool IsConfigured(string? semester)
    {
        return !string.IsNullOrWhiteSpace(semester) && _semesterService.GetStartDate(semester) is not null;
    }

    /// <inheritdoc />
    public async Task<bool> LoadAsync(string? semester, CancellationToken cancellationToken = default)
    {
        // 学期设置必须先就绪：未配置起始日时本模块整体禁用
        await _semesterService.LoadAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(semester) || !IsConfigured(semester))
        {
            _cache.Clear();
            _cachedSemester = semester;
            _loaded = true;
            return false;
        }

        var items = await _repository.GetBySemesterAsync(semester, cancellationToken);

        _cache.Clear();
        _cache.AddRange(items);
        _cachedSemester = semester;
        _loaded = true;
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<ScheduleAdjustment> GetCached()
    {
        return _cache;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ScheduleAdjustment>> GetBySemesterAsync(
        string semester,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetBySemesterAsync(semester, cancellationToken);
    }

    /// <inheritdoc />
    public AdjustmentDayPlan? GetDayPlan(string? semester, DateOnly date)
    {
        if (!_loaded || !IsConfigured(semester) || _cachedSemester != semester)
        {
            return null;
        }

        var holidays = new List<ScheduleAdjustment>();
        var cancellations = new List<ScheduleAdjustment>();
        var makeUps = new List<ScheduleAdjustment>();
        int? swapWeekday = null;

        foreach (var item in _cache)
        {
            if (item.AffectedDate != date)
            {
                continue;
            }

            switch (item.AdjustmentType)
            {
                case ScheduleAdjustmentTypes.Holiday:
                    holidays.Add(item);
                    break;
                case ScheduleAdjustmentTypes.SwapDay:
                    swapWeekday = item.FollowWeekday;
                    break;
                case ScheduleAdjustmentTypes.Cancelled:
                    cancellations.Add(item);
                    break;
                case ScheduleAdjustmentTypes.MakeUp:
                    makeUps.Add(item);
                    break;
            }
        }

        return new AdjustmentDayPlan(date, holidays.Count > 0, swapWeekday, cancellations, makeUps);
    }

    /// <inheritdoc />
    public IReadOnlyList<AdjustmentDayPlan> GetWeekPlans(string? semester, int week)
    {
        if (!_loaded || !IsConfigured(semester) || _cachedSemester != semester)
        {
            return Array.Empty<AdjustmentDayPlan>();
        }

        var dates = _semesterService.GetDatesOfWeek(semester!, week);
        if (dates.Count == 0)
        {
            return Array.Empty<AdjustmentDayPlan>();
        }

        var plans = new List<AdjustmentDayPlan>(dates.Count);
        foreach (var date in dates)
        {
            var plan = GetDayPlan(semester, date);
            if (plan is not null)
            {
                plans.Add(plan);
            }
        }

        return plans;
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveRangeAsync(
        IReadOnlyList<ScheduleAdjustment> adjustments,
        CancellationToken cancellationToken = default)
    {
        var result = await _repository.AddRangeAsync(adjustments, cancellationToken);
        if (!result.Success)
        {
            return result;
        }

        if (adjustments.Count > 0 && _cachedSemester == adjustments[0].Semester)
        {
            await LoadAsync(_cachedSemester, cancellationToken);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return OperationResult.Ok();
    }

    /// <inheritdoc />
    public async Task<OperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _repository.DeleteAsync(id, cancellationToken);
        if (!result.Success)
        {
            return result;
        }

        if (_loaded)
        {
            await LoadAsync(_cachedSemester, cancellationToken);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return OperationResult.Ok();
    }

    /// <inheritdoc />
    public async Task<OperationResult> UpdateAsync(
        ScheduleAdjustment adjustment,
        CancellationToken cancellationToken = default)
    {
        var result = await _repository.UpdateAsync(adjustment, cancellationToken);
        if (!result.Success)
        {
            return result;
        }

        if (_loaded)
        {
            await LoadAsync(_cachedSemester, cancellationToken);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return OperationResult.Ok();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SemesterCourseOption>> GetCoursesAsync(
        string semester,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetCoursesBySemesterAsync(semester, cancellationToken);
    }

    /// <summary>
    /// 星期显示文本（供模块页展示），统一走模型里的常量表，避免各处硬编码。
    /// </summary>
    public static string WeekdayText(int weekday)
    {
        return ScheduleAdjustmentTypes.GetWeekdayText(weekday);
    }

    /// <summary>
    /// 日期展示文本（yyyy-MM-dd）。
    /// </summary>
    public static string DateText(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
