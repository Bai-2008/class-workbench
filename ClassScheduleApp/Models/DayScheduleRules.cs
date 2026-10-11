namespace ClassScheduleApp.Models;

/// <summary>
/// 参与「某日期有效课程」计算的候选时段（来自学生课表项或空闲统计的占用行），
/// 其周次范围与单双周已由调用方按目标周过滤。
/// </summary>
public sealed class DayScheduleSlot
{
    public int CourseId { get; set; }

    /// <summary>学生 Id（空闲统计按学生+课程做补课替换；课表网格为 0）。</summary>
    public int StudentId { get; set; }

    /// <summary>单双周展示文本（课表网格小标用；每周为空）。</summary>
    public string ParityText { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public string Teacher { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    /// <summary>星期 1–7。</summary>
    public int DayOfWeek { get; set; }

    public int StartPeriod { get; set; }

    public int EndPeriod { get; set; }
}

/// <summary>叠加调整后某日的一个有效时段。</summary>
public sealed class DayScheduleEntry
{
    public int CourseId { get; set; }

    /// <summary>学生 Id（空闲统计口径；课表网格为 0）。</summary>
    public int StudentId { get; set; }

    /// <summary>单双周展示文本（课表网格小标用；每周为空）。</summary>
    public string ParityText { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public string Teacher { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public int DayOfWeek { get; set; }

    public int StartPeriod { get; set; }

    public int EndPeriod { get; set; }

    /// <summary>该格来自补课记录。</summary>
    public bool IsMakeUp { get; set; }

    /// <summary>该格已被停课（保留展示但弱化）。</summary>
    public bool IsCancelled { get; set; }
}

/// <summary>某日的计算结论（渲染输入层：有效时段集合 + 标记 + 降级说明）。</summary>
public sealed class DayScheduleResult
{
    /// <summary>星期 1–7。</summary>
    public int Day { get; set; }

    /// <summary>是否放假（整天无课）。</summary>
    public bool IsHoliday { get; set; }

    /// <summary>调休上课日：按周几的课表上课（null = 非调休日）。</summary>
    public int? SwapWeekday { get; set; }

    /// <summary>该日实际取课的星期（调休日 = FollowWeekday，否则为自身）。</summary>
    public int EffectiveWeekday { get; set; }

    /// <summary>列标记文本：放假 / 上周X课 / 空。</summary>
    public string MarkText { get; set; } = string.Empty;

    /// <summary>有效时段。</summary>
    public List<DayScheduleEntry> Entries { get; set; } = new();
}

/// <summary>
/// 「某日期有效课程」的纯计算内核（无状态、无 IO）：星期匹配 + 调整叠加。
/// 周次范围与单双周判断转调 <see cref="WeekParityTypes"/>，本类不复制奇偶语义。
/// 调整记录本体由 <c>IScheduleAdjustmentRepository</c> 提供，本类只做判定。
/// </summary>
public static class DayScheduleRules
{
    /// <summary>某时段的周次 + 单双周是否在目标周生效（唯一真相源 → WeekParityTypes）。</summary>
    public static bool IsSlotActiveInWeek(int startWeek, int endWeek, int weekParity, int week)
        => WeekParityTypes.IsActiveInWeek(startWeek, endWeek, weekParity, week);

    /// <summary>列标记文本（与 AdjustmentDayPlan.MarkText 同源口径）。</summary>
    public static string BuildMarkText(bool isHoliday, int? swapWeekday)
        => isHoliday
            ? "放假"
            : swapWeekday.HasValue ? $"上周{ScheduleAdjustmentTypes.GetWeekdayText(swapWeekday.Value)[1..]}课" : string.Empty;

    /// <summary>
    /// 计算某日的有效时段。
    /// </summary>
    /// <param name="day">星期 1–7。</param>
    /// <param name="isHoliday">该日是否放假。</param>
    /// <param name="swapWeekday">调休上课日按周几上课。</param>
    /// <param name="cancellations">该日停课记录。</param>
    /// <param name="makeUps">该日补课记录（含课程名/教师/教室与节次）。</param>
    /// <param name="slots">候选时段（已按周过滤）。</param>
    /// <param name="periodAwareCancellation">
    /// 停课是否按节次区间匹配：空闲统计口径为 true（只剔除与停课节次重叠的占用行）；
    /// 课表网格口径为 false（按课程整体标记停课）。两处既有语义不同，此处如实保留，不擅自统一。
    /// </param>
    /// <param name="replaceExistingOnMakeUp">
    /// 遇到补课时是否先移除该课程当日原有格：空闲统计口径为 true（补课取代原占用，按课程整体移除）；
    /// 课表网格口径为 false（补课直接追加一格）。同为既有语义差异，如实保留。
    /// </param>
    public static DayScheduleResult Calculate(
        int day,
        bool isHoliday,
        int? swapWeekday,
        IReadOnlyList<ScheduleAdjustment> cancellations,
        IReadOnlyList<MakeUpSlot> makeUps,
        IEnumerable<DayScheduleSlot> slots,
        bool periodAwareCancellation,
        bool replaceExistingOnMakeUp)
    {
        var result = new DayScheduleResult
        {
            Day = day,
            IsHoliday = isHoliday,
            SwapWeekday = swapWeekday,
            EffectiveWeekday = swapWeekday ?? day,
            MarkText = BuildMarkText(isHoliday, swapWeekday),
        };

        if (!isHoliday)
        {
            foreach (var slot in slots)
            {
                if (slot.DayOfWeek != result.EffectiveWeekday)
                {
                    continue;
                }

                result.Entries.Add(new DayScheduleEntry
                {
                    CourseId = slot.CourseId,
                    StudentId = slot.StudentId,
                    ParityText = slot.ParityText,
                    CourseName = slot.CourseName,
                    Teacher = slot.Teacher,
                    Location = slot.Location,
                    DayOfWeek = day,
                    StartPeriod = slot.StartPeriod,
                    EndPeriod = slot.EndPeriod,
                });
            }
        }

        // 停课
        foreach (var cancel in cancellations)
        {
            foreach (var entry in result.Entries)
            {
                if (entry.CourseId != cancel.CourseId || entry.IsMakeUp)
                {
                    continue;
                }

                if (!periodAwareCancellation)
                {
                    entry.IsCancelled = true;
                    continue;
                }

                var cancelStart = cancel.StartPeriod ?? entry.StartPeriod;
                var cancelEnd = cancel.EndPeriod ?? entry.EndPeriod;
                if (entry.StartPeriod <= cancelEnd && entry.EndPeriod >= cancelStart)
                {
                    entry.IsCancelled = true;
                }
            }
        }

        // 补课
        foreach (var makeup in makeUps)
        {
            if (makeup.StartPeriod is null || makeup.EndPeriod is null)
            {
                continue;
            }

            if (replaceExistingOnMakeUp)
            {
                result.Entries.RemoveAll(entry => !entry.IsCancelled
                    && entry.CourseId == (makeup.CourseId ?? 0)
                    && (makeup.StudentId == 0 || entry.StudentId == makeup.StudentId));
            }

            result.Entries.Add(new DayScheduleEntry
            {
                CourseId = makeup.CourseId ?? 0,
                StudentId = makeup.StudentId,
                CourseName = makeup.CourseName,
                Teacher = makeup.Teacher,
                Location = makeup.Location,
                DayOfWeek = day,
                StartPeriod = makeup.StartPeriod.Value,
                EndPeriod = makeup.EndPeriod.Value,
                IsMakeUp = true,
            });
        }

        return result;
    }
}

/// <summary>补课记录展开成时段所需的展示信息（由调用方从课程/学生数据补齐）。</summary>
public sealed class MakeUpSlot
{
    public int? CourseId { get; set; }

    /// <summary>学生 Id（空闲统计口径：补课替换同学生+同课程的原有格；课表网格不填）。</summary>
    public int StudentId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public string Teacher { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public int? StartPeriod { get; set; }

    public int? EndPeriod { get; set; }
}
