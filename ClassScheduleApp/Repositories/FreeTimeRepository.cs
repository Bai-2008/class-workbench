using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 空闲时间统计的数据访问实现。每个方法内创建并释放短生命周期 DbContext。
/// 所有"有课"判定只用 StartWeek / EndWeek（WeekRange 已废弃，禁止用于时间计算）。
/// </summary>
public sealed class FreeTimeRepository : IFreeTimeRepository
{
    /// <summary>6 个节次对：(起始节次, 结束节次)。</summary>
    /// <summary>矩阵行槽位：两节一组（最后一组可落单），按作息表的实际节数动态生成。</summary>
    private static (int Start, int End)[] BuildPeriodSlots(int periodCount)
    {
        var slots = new List<(int Start, int End)>();
        for (var start = 1; start <= periodCount; start += 2)
        {
            slots.Add((start, Math.Min(start + 1, periodCount)));
        }

        return slots.ToArray();
    }

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IScheduleAdjustmentRepository _adjustmentRepository;

    public FreeTimeRepository(IDbContextFactory<AppDbContext> factory, IScheduleAdjustmentRepository adjustmentRepository)
    {
        _factory = factory;
        _adjustmentRepository = adjustmentRepository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetSemestersAsync(CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        // 学期列表 = 「已配置学期（SemesterSettings）」∪「出现过选课的学期」。
        // 必须并入已配置学期：清空课表 / 新建库时选课为空，若只按选课推导，
        // 学期下拉会变空，导致课表导入、空闲统计、调休补课、设置页都无法选学期。
        return await ctx.StudentCourses
            .AsNoTracking()
            .Select(sc => sc.Semester)
            .Union(ctx.SemesterSettings.AsNoTracking().Select(s => s.SemesterName))
            .OrderByDescending(semester => semester)
            .ToListAsync(cancellationToken);
    }

    /// <summary>一条「某学生某时段有课」记录（叠加调休补课用，含课程 Id 与节次）。</summary>
    private sealed class BusyRow
    {
        public int StudentId { get; set; }

        public string StudentNo { get; set; } = string.Empty;

        public string StudentName { get; set; } = string.Empty;

        public int CourseId { get; set; }

        public string CourseName { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        public int DayOfWeek { get; set; }

        /// <summary>周次范围与单双周（周过滤统一走 WeekParityTypes.IsActiveInWeek）。</summary>
        public int StartWeek { get; set; }

        public int EndWeek { get; set; }

        public int WeekParity { get; set; }

        public int StartPeriod { get; set; }

        public int EndPeriod { get; set; }

        /// <summary>补课行的日历日期；排课行为 null。</summary>
        public DateOnly? Date { get; set; }
    }

    /// <summary>该周用于叠加计算的原始数据。</summary>
    private sealed class WeekFreeData
    {
        public int Week { get; set; }

        /// <summary>学期第 1 周周一；为 null 表示未配置（不叠加）。</summary>
        public DateOnly? StartDate { get; set; }

        public List<BusyRow> Entries { get; set; } = new();

        public List<ScheduleAdjustment> Adjustments { get; set; } = new();

        /// <summary>补课展开行（该课程在该学期的在读学生）。</summary>
        public List<BusyRow> MakeUps { get; set; } = new();
    }

    /// <summary>
    /// 载入该周的排课与调整数据。起始日未配置时不载入调整 = 不做叠加（与模块禁用一致）。
    /// </summary>
    private async Task<WeekFreeData> LoadWeekDataAsync(
        AppDbContext ctx,
        string semester,
        int week,
        CancellationToken cancellationToken)
    {
        var data = new WeekFreeData { Week = week };

        data.StartDate = await ctx.SemesterSettings
            .AsNoTracking()
            .Where(s => s.SemesterName == semester)
            .Select(s => s.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        data.Entries = await (
                from sc in ctx.StudentCourses.AsNoTracking()
                where sc.Semester == semester
                join cs in ctx.CourseSchedules.AsNoTracking() on sc.CourseId equals cs.CourseId
                join c in ctx.Courses.AsNoTracking() on cs.CourseId equals c.Id
                join s in ctx.Students.AsNoTracking() on sc.StudentId equals s.Id
                orderby s.StudentNo
                select new BusyRow
                {
                    StudentId = s.Id,
                    StudentNo = s.StudentNo,
                    StudentName = s.Name,
                    CourseId = c.Id,
                    CourseName = c.CourseName,
                    Location = cs.Location,
                    DayOfWeek = cs.DayOfWeek,
                    StartPeriod = cs.StartPeriod,
                    EndPeriod = cs.EndPeriod,
                    StartWeek = cs.StartWeek,
                    EndWeek = cs.EndWeek,
                    WeekParity = cs.WeekParity,
                })
            .ToListAsync(cancellationToken);

        // 周次范围 + 单双周在内存里做：统一调用 WeekParityTypes.IsActiveInWeek（全项目唯一谓词），
        // 避免把奇偶判断复制进 SQL。一个学期的排课行数量很小，代价可忽略。
        data.Entries.RemoveAll(row => !WeekParityTypes.IsActiveInWeek(row.StartWeek, row.EndWeek, row.WeekParity, week));

        if (data.StartDate is null)
        {
            return data;
        }

        var first = data.StartDate.Value.AddDays((week - 1) * 7);
        var last = first.AddDays(6);

        // 调整记录不再由本仓储直接查表：统一经 IScheduleAdjustmentRepository（调整表唯一数据入口）
        data.Adjustments = (await _adjustmentRepository.GetByDateRangeAsync(first, last, cancellationToken))
            .Where(a => a.Semester == semester)
            .ToList();

        // 补课：预先展开为「该课程在该学期的在读学生」行
        foreach (var adjustment in data.Adjustments)
        {
            if (adjustment.AdjustmentType != ScheduleAdjustmentTypes.MakeUp
                || adjustment.CourseId is null
                || adjustment.StartPeriod is null
                || adjustment.EndPeriod is null)
            {
                continue;
            }

            var enrolled = await (
                    from sc in ctx.StudentCourses.AsNoTracking()
                    join s in ctx.Students.AsNoTracking() on sc.StudentId equals s.Id
                    join c in ctx.Courses.AsNoTracking() on sc.CourseId equals c.Id
                    where sc.Semester == semester && sc.CourseId == adjustment.CourseId.Value
                    select new { s.Id, s.StudentNo, s.Name, c.CourseName })
                .ToListAsync(cancellationToken);

            foreach (var student in enrolled)
            {
                data.MakeUps.Add(new BusyRow
                {
                    StudentId = student.Id,
                    StudentNo = student.StudentNo,
                    StudentName = student.Name,
                    CourseId = adjustment.CourseId.Value,
                    CourseName = student.CourseName,
                    Location = adjustment.NewLocation ?? string.Empty,
                    DayOfWeek = adjustment.AffectedDate.DayOfWeek == DayOfWeek.Sunday
                        ? 7
                        : (int)adjustment.AffectedDate.DayOfWeek,
                    StartPeriod = adjustment.StartPeriod.Value,
                    EndPeriod = adjustment.EndPeriod.Value,
                    Date = adjustment.AffectedDate,
                });
            }
        }

        return data;
    }

    /// <summary>
    /// 叠加规则：放假 → 该时段全班空闲；调休 → 按 FollowWeekday 匹配课表；
    /// 停课 → 从有课集合剔除；补课 → 加入有课集合。
    /// </summary>
    private static List<BusyRow> BusyRowsFor(WeekFreeData data, int day, int startPeriod, int endPeriod)
    {
        // 星期按**真实星期几**对齐：起始日不一定是周一时，(周-1)*7+天-1 会把整列日期错位一天。
        // 取法：该教学周窗口（起始日 + (周-1)*7 起 7 天）内、真实星期几等于 day 的那一天（周一=1…周日=7）
        DateOnly? date = null;
        if (data.StartDate is { } weekStartBase)
        {
            var weekStart = weekStartBase.AddDays((data.Week - 1) * 7);
            var weekStartWeekday = weekStart.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)weekStart.DayOfWeek;
            date = weekStart.AddDays((((day - weekStartWeekday) % 7) + 7) % 7);
        }

        var dayAdjustments = date.HasValue
            ? data.Adjustments.Where(a => a.AffectedDate == date.Value).ToList()
            : new List<ScheduleAdjustment>();

        var isHoliday = dayAdjustments.Any(a => a.AdjustmentType == ScheduleAdjustmentTypes.Holiday);

        int? swapWeekday = null;
        foreach (var adjustment in dayAdjustments)
        {
            if (adjustment.AdjustmentType == ScheduleAdjustmentTypes.SwapDay && adjustment.FollowWeekday.HasValue)
            {
                swapWeekday = adjustment.FollowWeekday.Value;
            }
        }

        var effectiveDay = swapWeekday ?? day;

        // 候选时段：星期匹配 + 节次窗口重叠（与既有语义一致）
        var sources = new Dictionary<(int CourseId, int StudentId, int Start, int End), BusyRow>();
        var slots = new List<DayScheduleSlot>();
        foreach (var entry in data.Entries)
        {
            if (entry.DayOfWeek != effectiveDay || entry.StartPeriod > endPeriod || entry.EndPeriod < startPeriod)
            {
                continue;
            }

            slots.Add(new DayScheduleSlot
            {
                CourseId = entry.CourseId,
                StudentId = entry.StudentId,
                CourseName = entry.CourseName,
                Location = entry.Location,
                DayOfWeek = entry.DayOfWeek,
                StartPeriod = entry.StartPeriod,
                EndPeriod = entry.EndPeriod,
            });

            sources[(entry.CourseId, entry.StudentId, entry.StartPeriod, entry.EndPeriod)] = entry;
        }

        var makeUps = new List<MakeUpSlot>();
        foreach (var makeup in data.MakeUps)
        {
            if (makeup.Date != date || makeup.StartPeriod > endPeriod || makeup.EndPeriod < startPeriod)
            {
                continue;
            }

            makeUps.Add(new MakeUpSlot
            {
                CourseId = makeup.CourseId,
                StudentId = makeup.StudentId,
                CourseName = makeup.CourseName,
                Location = makeup.Location,
                StartPeriod = makeup.StartPeriod,
                EndPeriod = makeup.EndPeriod,
            });

            sources[(makeup.CourseId, makeup.StudentId, makeup.StartPeriod, makeup.EndPeriod)] = makeup;
        }

        // 叠加规则走共享内核（单一真相源）：停课按节次区间剔除、补课替换同学生同课程、放假整体为空
        var result = DayScheduleRules.Calculate(
            day,
            isHoliday,
            swapWeekday,
            dayAdjustments.Where(a => a.AdjustmentType == ScheduleAdjustmentTypes.Cancelled).ToList(),
            makeUps,
            slots,
            periodAwareCancellation: true,
            replaceExistingOnMakeUp: true);

        var rows = new List<BusyRow>();
        foreach (var entry in result.Entries)
        {
            if (entry.IsCancelled)
            {
                continue;
            }

            if (sources.TryGetValue((entry.CourseId, entry.StudentId, entry.StartPeriod, entry.EndPeriod), out var source))
            {
                rows.Add(source);
            }
        }

        return rows;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BusyStudentInfo>> GetBusyStudentsAsync(
        string semester,
        int week,
        int day,
        int startPeriod,
        int endPeriod,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var data = await LoadWeekDataAsync(ctx, semester, week, cancellationToken);
        var rows = BusyRowsFor(data, day, startPeriod, endPeriod);

        var result = new List<BusyStudentInfo>();
        foreach (var row in rows)
        {
            result.Add(new BusyStudentInfo
            {
                StudentId = row.StudentId,
                StudentNo = row.StudentNo,
                StudentName = row.StudentName,
                CourseName = row.CourseName,
                Location = row.Location,
            });
        }

        result.Sort(static (left, right) => string.Compare(left.StudentNo, right.StudentNo, StringComparison.Ordinal));
        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Student>> GetFreeStudentsAsync(
        string semester,
        int week,
        int day,
        int startPeriod,
        int endPeriod,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var data = await LoadWeekDataAsync(ctx, semester, week, cancellationToken);
        var rows = BusyRowsFor(data, day, startPeriod, endPeriod);

        var busyStudentIds = new HashSet<int>();
        foreach (var row in rows)
        {
            busyStudentIds.Add(row.StudentId);
        }

        var students = await ctx.Students
            .AsNoTracking()
            .OrderBy(s => s.StudentNo)
            .ToListAsync(cancellationToken);

        var free = new List<Student>();
        foreach (var student in students)
        {
            if (!busyStudentIds.Contains(student.Id))
            {
                free.Add(student);
            }
        }

        return free;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClassFreeCell>> GetClassFreeMatrixAsync(
        string semester,
        int week,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        // 分母：Students 表全部学生
        var students = await ctx.Students
            .AsNoTracking()
            .OrderBy(s => s.StudentNo)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(cancellationToken);

        // 该周的排课 + 调休补课调整（起始日未配置时不叠加，与原行为一致）
        var data = await LoadWeekDataAsync(ctx, semester, week, cancellationToken);

        // 行槽位按作息表实际节数生成（设置页可改节数）
        var periodCount = await ctx.PeriodTimes.CountAsync(cancellationToken);
        var periodSlots = BuildPeriodSlots(periodCount > 0 ? periodCount : 12);

        var cells = new List<ClassFreeCell>();
        for (var day = 1; day <= 7; day++)
        {
            for (var slotIndex = 0; slotIndex < periodSlots.Length; slotIndex++)
            {
                var (slotStart, slotEnd) = periodSlots[slotIndex];

                var busyStudentIds = new HashSet<int>();
                foreach (var row in BusyRowsFor(data, day, slotStart, slotEnd))
                {
                    busyStudentIds.Add(row.StudentId);
                }

                var freeNames = new List<string>();
                foreach (var student in students)
                {
                    if (!busyStudentIds.Contains(student.Id))
                    {
                        freeNames.Add(student.Name);
                    }
                }

                cells.Add(new ClassFreeCell
                {
                    DayOfWeek = day,
                    SlotIndex = slotIndex,
                    StartPeriod = slotStart,
                    EndPeriod = slotEnd,
                    FreeCount = freeNames.Count,
                    TotalCount = students.Count,
                    FreeStudentNames = freeNames,
                });
            }
        }

        return cells;
    }
}
