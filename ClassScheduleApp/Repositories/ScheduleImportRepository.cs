using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using ClassScheduleApp.Services.ScheduleImport;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 课表导入的数据访问实现：整体覆盖某生某学期选课，按「开课班」判定课程同一性，
/// 并做共享保护与孤儿课程清理。
/// </summary>
public sealed class ScheduleImportRepository : IScheduleImportRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ScheduleImportRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Student>> FindStudentsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await ctx.Students
            .AsNoTracking()
            .Where(s => s.Name == name)
            .OrderBy(s => s.StudentNo)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Student?> GetStudentByNoAsync(string studentNo, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await ctx.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentNo == studentNo, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Student>> GetAllStudentsAsync(CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await ctx.Students
            .AsNoTracking()
            .OrderBy(s => s.StudentNo)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ScheduleImportOutcome> ApplyStudentScheduleAsync(
        int studentId,
        string semester,
        IReadOnlyList<ParsedScheduleEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var outcome = new ScheduleImportOutcome();

        using var ctx = _factory.CreateDbContext();
        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);

        // 1) 按「课程名 + 教师」分组（同一门课在一份文件里可能出现多条时段）
        var groups = new List<(string Name, string Teacher, List<ParsedScheduleEntry> Slots)>();
        foreach (var entry in entries)
        {
            var group = groups.FirstOrDefault(g => g.Name == entry.CourseName && g.Teacher == entry.Teacher);
            if (group.Slots is null)
            {
                group = (entry.CourseName, entry.Teacher, new List<ParsedScheduleEntry>());
                groups.Add(group);
            }

            group.Slots.Add(entry);
        }

        // 2) 覆盖前：该生本学期的既有课程关联
        var previousCourseIds = await ctx.StudentCourses
            .Where(sc => sc.StudentId == studentId && sc.Semester == semester)
            .Select(sc => sc.CourseId)
            .ToListAsync(cancellationToken);

        var existingLinks = await ctx.StudentCourses
            .Where(sc => sc.StudentId == studentId && sc.Semester == semester)
            .ToListAsync(cancellationToken);
        ctx.StudentCourses.RemoveRange(existingLinks);

        // 3) 逐门解析开课班：时段集合完全一致才复用，否则新建（绝不改既有课程时段）
        var targetCourseIds = new List<int>();
        foreach (var group in groups)
        {
            var wantedKeys = SlotKeysOf(group.Slots);

            var candidates = await ctx.Courses
                .Include(c => c.CourseSchedules)
                .Where(c => c.CourseName == group.Name && c.Teacher == group.Teacher)
                .ToListAsync(cancellationToken);

            var matched = candidates.FirstOrDefault(c => SlotKeysOf(c.CourseSchedules).SetEquals(wantedKeys));

            if (matched is null)
            {
                var course = new Course
                {
                    CourseNo = group.Slots[0].CourseNo,
                    CourseName = group.Name,
                    Teacher = group.Teacher,
                    Credit = group.Slots[0].Credit,
                };
                ctx.Courses.Add(course);
                await ctx.SaveChangesAsync(cancellationToken);

                foreach (var slot in group.Slots)
                {
                    ctx.CourseSchedules.Add(new CourseSchedule
                    {
                        CourseId = course.Id,
                        DayOfWeek = slot.DayOfWeek,
                        StartPeriod = slot.StartPeriod,
                        EndPeriod = slot.EndPeriod,
                        StartWeek = slot.StartWeek,
                        EndWeek = slot.EndWeek,
                        WeekParity = slot.WeekParity,
                        WeekRange = $"{slot.StartWeek}-{slot.EndWeek}",
                        Location = slot.Location,
                    });
                }

                await ctx.SaveChangesAsync(cancellationToken);
                matched = course;
                outcome.CreatedCourses.Add($"{group.Name}（{group.Teacher}）：{DescribeSlots(group.Slots)}");

                // 同名同师、时段不一致 → 属于不同开课班，逐条列出与哪些既有班次有交集
                foreach (var other in candidates)
                {
                    var otherKeys = SlotKeysOf(other.CourseSchedules);
                    if (otherKeys.SetEquals(wantedKeys))
                    {
                        continue;
                    }

                    var overlapped = SlotsOverlap(group.Slots, other.CourseSchedules);
                    outcome.CourseSplits.Add(
                        $"{group.Name}（{group.Teacher}）：新建开课班 {DescribeSlotKeys(wantedKeys)}" +
                        $" ←→ 既有开课班 {DescribeSlotKeys(otherKeys)}" +
                        (overlapped ? "｜时段部分重叠，请人工核对" : "｜时段不重叠"));
                }
            }
            else
            {
                outcome.LinkedExistingCount++;
            }

            targetCourseIds.Add(matched.Id);

            ctx.StudentCourses.Add(new StudentCourse
            {
                StudentId = studentId,
                CourseId = matched.Id,
                Semester = semester,
            });
        }

        await ctx.SaveChangesAsync(cancellationToken);

        // 4) 孤儿课程清理：本次被解除关联、且现在「零关联」的课程连同时段删除；
        //    仍被其他学生（或其它学期）关联的课程不动（共享保护）
        foreach (var courseId in previousCourseIds.Distinct())
        {
            if (targetCourseIds.Contains(courseId))
            {
                continue;
            }

            if (await ctx.StudentCourses.AnyAsync(sc => sc.CourseId == courseId, cancellationToken))
            {
                continue;
            }

            var orphan = await ctx.Courses
                .Include(c => c.CourseSchedules)
                .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);
            if (orphan is null)
            {
                continue;
            }

            outcome.DeletedOrphanCourses.Add($"{orphan.CourseName}（{orphan.Teacher}）");
            ctx.CourseSchedules.RemoveRange(orphan.CourseSchedules);
            ctx.Courses.Remove(orphan);
        }

        await ctx.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return outcome;
    }

    /// <summary>
    /// 时段集合归一化：每条时段取「星期|起止节次|起止周|单双周」为键，
    /// 集合比较与顺序无关（同一门课在一份文件里的多条时段不分先后）。
    /// </summary>
    private static HashSet<string> SlotKeysOf(IEnumerable<ParsedScheduleEntry> slots)
        => slots.Select(s => SlotKey(s.DayOfWeek, s.StartPeriod, s.EndPeriod, s.StartWeek, s.EndWeek, s.WeekParity)).ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> SlotKeysOf(IEnumerable<CourseSchedule> slots)
        => slots.Select(s => SlotKey(s.DayOfWeek, s.StartPeriod, s.EndPeriod, s.StartWeek, s.EndWeek, s.WeekParity)).ToHashSet(StringComparer.Ordinal);

    private static string SlotKey(int day, int startPeriod, int endPeriod, int startWeek, int endWeek, int parity)
        => $"{day}|{startPeriod}-{endPeriod}|{startWeek}-{endWeek}|{parity}";

    /// <summary>把导入时段渲染成人类可读的摘要（用于报告）。</summary>
    private static string DescribeSlots(IEnumerable<ParsedScheduleEntry> slots)
        => string.Join("、", slots
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartPeriod)
            .Select(s => DescribeSlotKey(SlotKey(s.DayOfWeek, s.StartPeriod, s.EndPeriod, s.StartWeek, s.EndWeek, s.WeekParity))));

    /// <summary>
    /// 两组时段是否有实际交集：星期相同、节次区间相交、周次区间相交，且单双周相容
    /// （每周与单/双周都相容；单周与双周永不相交）。用于报告里的「时段部分重叠」提示。
    /// </summary>
    private static bool SlotsOverlap(
        IReadOnlyList<ParsedScheduleEntry> imported,
        IEnumerable<CourseSchedule> existing)
    {
        foreach (var a in imported)
        {
            foreach (var b in existing)
            {
                if (a.DayOfWeek != b.DayOfWeek)
                {
                    continue;
                }

                if (a.StartPeriod > b.EndPeriod || a.EndPeriod < b.StartPeriod)
                {
                    continue;
                }

                if (a.StartWeek > b.EndWeek || a.EndWeek < b.StartWeek)
                {
                    continue;
                }

                if (ParityCompatible(a.WeekParity, b.WeekParity))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ParityCompatible(int left, int right)
        => left == WeekParityTypes.Every || right == WeekParityTypes.Every || left == right;

    private static string DescribeSlotKeys(IEnumerable<string> keys)
        => string.Join("、", keys.OrderBy(k => k, StringComparer.Ordinal).Select(DescribeSlotKey));

    /// <summary>把归一化键还原成「周一 1-2节 1-16周/单周」。</summary>
    private static string DescribeSlotKey(string key)
    {
        var parts = key.Split('|');
        if (parts.Length != 4)
        {
            return key;
        }

        var day = int.Parse(parts[0]);
        var parity = int.Parse(parts[3]);
        return $"{ScheduleAdjustmentTypes.GetWeekdayText(day)} {parts[1]}节 {parts[2]}周" +
            (parity == WeekParityTypes.Every ? string.Empty : "/" + WeekParityTypes.ToDisplay(parity));
    }
}
