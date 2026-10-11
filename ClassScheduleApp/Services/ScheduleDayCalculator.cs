using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services;

/// <summary>
/// 共享计算服务实现：组合 <see cref="ISemesterService"/>（周序号）、
/// <see cref="IScheduleAdjustmentRepository"/>（调整表唯一读取点）与
/// <see cref="DayScheduleRules"/>（纯内核）。
/// </summary>
public sealed class ScheduleDayCalculator : IScheduleDayCalculator
{
    private readonly ISemesterService _semesterService;
    private readonly IScheduleAdjustmentRepository _adjustmentRepository;
    private readonly ISemesterSettingRepository _semesterSettingRepository;

    public ScheduleDayCalculator(
        ISemesterService semesterService,
        IScheduleAdjustmentRepository adjustmentRepository,
        ISemesterSettingRepository semesterSettingRepository)
    {
        _semesterService = semesterService;
        _adjustmentRepository = adjustmentRepository;
        _semesterSettingRepository = semesterSettingRepository;
    }

    /// <inheritdoc />
    public async Task<ScheduleDayContext> ResolveDayAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var settings = await _semesterSettingRepository.GetAllAsync(cancellationToken);

        foreach (var setting in settings.Where(s => s.StartDate is not null).OrderBy(s => s.StartDate))
        {
            var start = setting.StartDate!.Value;
            var weekCount = setting.WeekCount <= 0 ? 20 : setting.WeekCount;
            var offset = date.DayNumber - start.DayNumber;
            if (offset < 0 || offset >= weekCount * 7)
            {
                continue;
            }

            return new ScheduleDayContext
            {
                Semester = setting.SemesterName,
                Week = (offset / 7) + 1,
                WeekCount = weekCount,
                // 星期按**真实星期几**取（周一=1…周日=7）：起始日不一定是周一时，
                // 用「距起始日的第几天」会让整列星期错位一天（实测：起始日为周二时今天周日被算成周六）
                Weekday = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek,
                IsConfigured = true,
                IsOutsideTeachingWeeks = false,
            };
        }

        // 未落入任何已配置学期的区间：区分「完全没配置」与「配置了但日期在范围外」
        var anyConfigured = settings.Any(s => s.StartDate is not null);
        if (!anyConfigured && settings.Count > 0)
        {
            // 有学期但都没配起始日 → 降级：仅按星期过滤（此时按真实星期几兜底）
            var fallback = settings.OrderBy(s => s.SemesterName).Last();
            return new ScheduleDayContext
            {
                Semester = fallback.SemesterName,
                Week = null,
                WeekCount = fallback.WeekCount <= 0 ? 20 : fallback.WeekCount,
                Weekday = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek,
                IsConfigured = false,
                Hint = "该学期未配置起始日，仅按星期过滤（与课表网格降级一致）。",
            };
        }

        return new ScheduleDayContext
        {
            Semester = null,
            Week = null,
            IsConfigured = anyConfigured,
            IsOutsideTeachingWeeks = anyConfigured,
            Hint = anyConfigured
                ? "该日期不在任何教学周范围内，当天没有课程。"
                : "尚未配置任何学期起始日，无法判断该日期的课程。",
        };
    }

    /// <inheritdoc />
    public int? GetTeachingWeek(string? semester, DateOnly date)
        => string.IsNullOrWhiteSpace(semester) ? null : _semesterService.GetWeekOf(semester, date);

    /// <inheritdoc />
    public bool IsActiveInWeek(int startWeek, int endWeek, int weekParity, int week)
        => DayScheduleRules.IsSlotActiveInWeek(startWeek, endWeek, weekParity, week);

    /// <inheritdoc />
    public bool IsConfigured(string? semester)
        => !string.IsNullOrWhiteSpace(semester) && _semesterService.GetStartDate(semester) is not null;

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdjustmentDayPlan>> GetWeekDayPlansAsync(
        string? semester,
        int week,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(semester) || _semesterService.GetStartDate(semester!) is not { } startDate)
        {
            return Array.Empty<AdjustmentDayPlan>();
        }

        var weekStart = startDate.AddDays((week - 1) * 7);
        return await BuildPlansAsync(semester!, weekStart, weekStart.AddDays(6), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdjustmentDayPlan?> GetDayPlanAsync(
        string? semester,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(semester))
        {
            return null;
        }

        var plans = await BuildPlansAsync(semester!, date, date, cancellationToken);
        return plans.Count > 0 ? plans[0] : null;
    }

    /// <inheritdoc />
    public DayScheduleResult CalculateDay(
        int weekday,
        AdjustmentDayPlan? plan,
        IEnumerable<DayScheduleSlot> weekFilteredSlots,
        IReadOnlyList<MakeUpSlot> makeUpSlots,
        bool periodAwareCancellation = false,
        bool replaceExistingOnMakeUp = false)
    {
        var result = DayScheduleRules.Calculate(
            weekday,
            plan?.IsHoliday == true,
            plan?.SwapWeekday,
            plan?.Cancellations ?? Array.Empty<ScheduleAdjustment>(),
            makeUpSlots,
            weekFilteredSlots,
            periodAwareCancellation,
            replaceExistingOnMakeUp);

        // 无调整计划时保证标记为空（与既有渲染一致）
        if (plan is null)
        {
            result.MarkText = string.Empty;
        }

        return result;
    }

    /// <summary>按日期区间读取调整并展开成逐日计划（周一→周日的固定 7 天窗口）。</summary>
    private async Task<IReadOnlyList<AdjustmentDayPlan>> BuildPlansAsync(
        string semester,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var adjustments = await _adjustmentRepository.GetByDateRangeAsync(start, end, cancellationToken);

        var plans = new List<AdjustmentDayPlan>();
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var dayAdjustments = adjustments.Where(a => a.AffectedDate == date).ToList();

            var isHoliday = dayAdjustments.Any(a => a.AdjustmentType == ScheduleAdjustmentTypes.Holiday);
            int? swapWeekday = null;
            foreach (var adjustment in dayAdjustments)
            {
                if (adjustment.AdjustmentType == ScheduleAdjustmentTypes.SwapDay && adjustment.FollowWeekday.HasValue)
                {
                    swapWeekday = adjustment.FollowWeekday.Value;
                }
            }

            plans.Add(new AdjustmentDayPlan(
                date,
                isHoliday,
                swapWeekday,
                dayAdjustments.Where(a => a.AdjustmentType == ScheduleAdjustmentTypes.Cancelled).ToList(),
                dayAdjustments.Where(a => a.AdjustmentType == ScheduleAdjustmentTypes.MakeUp).ToList()));
        }

        return plans;
    }
}
