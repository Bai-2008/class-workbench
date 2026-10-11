using ClassScheduleApp.Models;

namespace ClassScheduleApp.Services;

/// <summary>
/// 「某日期有效课程」共享计算服务 —— 课表网格、课表列表、空闲统计、考勤登记四处共用的唯一真相源。
///
/// 职责边界：
/// · 教学周序号转调 <see cref="ISemesterService"/>，周次 + 单双周判断转调 <see cref="WeekParityTypes"/>，二者均不复制；
/// · 调整记录（放假日 / 停课 / 补课 / 调休上课日）的读取一并收敛于此（经 IScheduleAdjustmentRepository）；
/// · 日期 → 学期的解析与名单取数共用同一口径（不在此服务内二次定义学期）。
/// </summary>
public interface IScheduleDayCalculator
{
    /// <summary>日期 → 教学周序号（经 ISemesterService；起始日未配置时返回 null，即降级按星期过滤）。</summary>
    int? GetTeachingWeek(string? semester, DateOnly date);

    /// <summary>某时段的周次 + 单双周是否在目标周生效（唯一真相源 → WeekParityTypes）。</summary>
    bool IsActiveInWeek(int startWeek, int endWeek, int weekParity, int week);

    /// <summary>该学期是否已配置起始日（未配置 → 调整叠加整体禁用，与课表网格降级一致）。</summary>
    bool IsConfigured(string? semester);

    /// <summary>
    /// 日期 → 所属教学学期的统一解析（口径与名单取数共用）：按各学期起始日与周数判定，
    /// 不属于任何已配置学期时 Semester 为 null（调用方按既有降级处理：空列表 + 提示）。
    /// </summary>
    Task<ScheduleDayContext> ResolveDayAsync(DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取某周 7 天（周一→周日）的调整生效计划。起始日未配置或无调整时返回空集合（等于不叠加）。
    /// 内部按周日期区间一次性读取 ScheduleAdjustments，消费方不再各自查调整表。
    /// </summary>
    Task<IReadOnlyList<AdjustmentDayPlan>> GetWeekDayPlansAsync(
        string? semester,
        int week,
        CancellationToken cancellationToken = default);

    /// <summary>取某日的调整生效计划；未配置/无调整时返回 null。</summary>
    Task<AdjustmentDayPlan?> GetDayPlanAsync(
        string? semester,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 计算某日有效时段（渲染输入层）。
    /// </summary>
    /// <param name="weekday">星期 1–7。</param>
    /// <param name="plan">该日调整计划（null = 无调整）。</param>
    /// <param name="weekFilteredSlots">已按目标周（周次 + 单双周）过滤的候选时段。</param>
    /// <param name="makeUpSlots">该日补课记录展开后的展示信息（调用方补齐课程名/教师/教室）。</param>
    /// <param name="periodAwareCancellation">停课是否按节次区间匹配（空闲统计 true / 网格 false，见内核注释）。</param>
    /// <param name="replaceExistingOnMakeUp">补课是否移除原有同课程格（空闲统计 true / 网格 false）。</param>
    DayScheduleResult CalculateDay(
        int weekday,
        AdjustmentDayPlan? plan,
        IEnumerable<DayScheduleSlot> weekFilteredSlots,
        IReadOnlyList<MakeUpSlot> makeUpSlots,
        bool periodAwareCancellation = false,
        bool replaceExistingOnMakeUp = false);
}
