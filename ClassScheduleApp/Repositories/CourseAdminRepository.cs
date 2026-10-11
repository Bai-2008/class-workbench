using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 课程维护的数据访问实现。每个方法内创建并释放短生命周期 DbContext。
/// </summary>
public sealed class CourseAdminRepository : ICourseAdminRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public CourseAdminRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CourseDetail>> GetAllCoursesAsync(CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var courses = await ctx.Courses
            .AsNoTracking()
            .OrderBy(c => c.CourseNo)
            .ToListAsync(cancellationToken);

        var schedules = await ctx.CourseSchedules
            .AsNoTracking()
            .OrderBy(s => s.CourseId)
            .ThenBy(s => s.DayOfWeek)
            .ThenBy(s => s.StartPeriod)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        // 每门课的全部时段；列表展示与其它模块继续用第一条（按 Id 最小，保持既有行为）
        var schedulesByCourse = new Dictionary<int, List<CourseSchedule>>();
        foreach (var schedule in schedules)
        {
            if (!schedulesByCourse.TryGetValue(schedule.CourseId, out var list))
            {
                list = new List<CourseSchedule>();
                schedulesByCourse[schedule.CourseId] = list;
            }

            list.Add(schedule);
        }

        // 每门课的关联学生数（课程管理列表标注「无学生」用）
        var studentCounts = await ctx.StudentCourses
            .AsNoTracking()
            .GroupBy(sc => sc.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count, cancellationToken);

        var result = new List<CourseDetail>();
        foreach (var course in courses)
        {
            var detail = new CourseDetail
            {
                Id = course.Id,
                CourseNo = course.CourseNo,
                CourseName = course.CourseName,
                Teacher = course.Teacher,
                Credit = course.Credit,
            };

            if (schedulesByCourse.TryGetValue(course.Id, out var own))
            {
                var details = new List<CourseScheduleDetail>();
                CourseSchedule? first = null;
                foreach (var schedule in own)
                {
                    details.Add(ToDetail(schedule));
                    if (first is null || schedule.Id < first.Id)
                    {
                        first = schedule;
                    }
                }

                detail.Schedules = details;

                if (first is not null)
                {
                    detail.ScheduleId = first.Id;
                    detail.DayOfWeek = first.DayOfWeek;
                    detail.StartPeriod = first.StartPeriod;
                    detail.EndPeriod = first.EndPeriod;
                    detail.StartWeek = first.StartWeek;
                    detail.EndWeek = first.EndWeek;
                    detail.WeekParity = first.WeekParity;
                    detail.Location = first.Location;
                }
            }

            detail.StudentCount = studentCounts.TryGetValue(course.Id, out var studentCount) ? studentCount : 0;
            result.Add(detail);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> IsCourseNoTakenAsync(string courseNo, int? excludeCourseId = null, CancellationToken cancellationToken = default)
    {
        // 编号选填：留空不做查重（空编号存 NULL，唯一索引允许多个 NULL）
        if (string.IsNullOrWhiteSpace(courseNo))
        {
            return false;
        }

        using var ctx = _factory.CreateDbContext();

        var query = ctx.Courses.AsNoTracking().Where(c => c.CourseNo == courseNo);
        if (excludeCourseId.HasValue)
        {
            var excludeId = excludeCourseId.Value;
            query = query.Where(c => c.Id != excludeId);
        }

        return await query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult> AddCourseAsync(CourseDetail course, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var entity = new Course
        {
            CourseNo = NormalizeCourseNo(course.CourseNo),
            CourseName = course.CourseName,
            Teacher = course.Teacher,
            Credit = course.Credit,
        };

        // 多时段录入：整批写入；未提供时段时回退到旧版单时段字段
        foreach (var item in ResolveSchedules(course))
        {
            entity.CourseSchedules.Add(new CourseSchedule
            {
                DayOfWeek = item.DayOfWeek,
                StartPeriod = item.StartPeriod,
                EndPeriod = item.EndPeriod,
                StartWeek = item.StartWeek,
                EndWeek = item.EndWeek,
                WeekParity = item.WeekParity,
                WeekRange = $"{item.StartWeek}-{item.EndWeek}",
                Location = item.Location,
            });
        }

        ctx.Courses.Add(entity);

        try
        {
            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex) when (DbErrorInspector.IsUniqueConstraintViolation(ex))
        {
            return OperationResult.Fail($"课程编号 {course.CourseNo} 已存在，请更换后重试。");
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"保存课程失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult> UpdateCourseAsync(CourseDetail course, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var entity = await ctx.Courses
            .Include(c => c.CourseSchedules)
            .FirstOrDefaultAsync(c => c.Id == course.Id, cancellationToken);

        if (entity is null)
        {
            return OperationResult.Fail("该课程已不存在，可能已被其他操作删除。");
        }

        entity.CourseNo = NormalizeCourseNo(course.CourseNo);
        entity.CourseName = course.CourseName;
        entity.Teacher = course.Teacher;
        entity.Credit = course.Credit;

        // 多时段整体替换：按 Id 更新已有行、插入新行、删除不再存在的行
        var desired = ResolveSchedules(course);
        var keepIds = new HashSet<int>();

        foreach (var item in desired)
        {
            if (item.Id > 0)
            {
                CourseSchedule? target = null;
                foreach (var existing in entity.CourseSchedules)
                {
                    if (existing.Id == item.Id)
                    {
                        target = existing;
                        break;
                    }
                }

                if (target is not null)
                {
                    target.DayOfWeek = item.DayOfWeek;
                    target.StartPeriod = item.StartPeriod;
                    target.EndPeriod = item.EndPeriod;
                    target.StartWeek = item.StartWeek;
                    target.EndWeek = item.EndWeek;
                    target.WeekParity = item.WeekParity;
                    target.WeekRange = $"{item.StartWeek}-{item.EndWeek}";
                    target.Location = item.Location;
                    keepIds.Add(target.Id);
                    continue;
                }
            }

            entity.CourseSchedules.Add(new CourseSchedule
            {
                DayOfWeek = item.DayOfWeek,
                StartPeriod = item.StartPeriod,
                EndPeriod = item.EndPeriod,
                StartWeek = item.StartWeek,
                EndWeek = item.EndWeek,
                WeekParity = item.WeekParity,
                WeekRange = $"{item.StartWeek}-{item.EndWeek}",
                Location = item.Location,
            });
        }

        var removed = new List<CourseSchedule>();
        foreach (var existing in entity.CourseSchedules)
        {
            if (existing.Id > 0 && !keepIds.Contains(existing.Id) && !IsDesiredExisting(desired, existing.Id))
            {
                removed.Add(existing);
            }
        }

        foreach (var schedule in removed)
        {
            ctx.CourseSchedules.Remove(schedule);
        }

        try
        {
            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex) when (DbErrorInspector.IsUniqueConstraintViolation(ex))
        {
            return OperationResult.Fail($"课程编号 {course.CourseNo} 已被其他课程使用，请更换后重试。");
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"保存课程失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <summary>空编号统一存 NULL（唯一索引允许多个 NULL，避免多条空字符串互相冲突）。</summary>
    private static string? NormalizeCourseNo(string? courseNo)
    {
        return string.IsNullOrWhiteSpace(courseNo) ? null : courseNo.Trim();
    }

    /// <summary>目标行是否仍在本次提交的时段清单里（按 Id 命中）。</summary>
    private static bool IsDesiredExisting(IReadOnlyList<CourseScheduleDetail> desired, int id)
    {
        foreach (var item in desired)
        {
            if (item.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 取本次提交的时段清单：优先用多时段列表；为空时回退到旧版单时段字段，
    /// 保证既有调用方（课程列表的单时段编辑入口）行为不变。
    /// </summary>
    private static IReadOnlyList<CourseScheduleDetail> ResolveSchedules(CourseDetail course)
    {
        if (course.Schedules.Count > 0)
        {
            return course.Schedules;
        }

        if (course.DayOfWeek <= 0)
        {
            return Array.Empty<CourseScheduleDetail>();
        }

        return new List<CourseScheduleDetail>
        {
            new()
            {
                Id = course.ScheduleId,
                DayOfWeek = course.DayOfWeek,
                StartPeriod = course.StartPeriod,
                EndPeriod = course.EndPeriod,
                StartWeek = course.StartWeek,
                EndWeek = course.EndWeek,
                WeekParity = course.WeekParity,
                Location = course.Location,
            },
        };
    }

    private static CourseScheduleDetail ToDetail(CourseSchedule schedule)
    {
        return new CourseScheduleDetail
        {
            Id = schedule.Id,
            DayOfWeek = schedule.DayOfWeek,
            StartPeriod = schedule.StartPeriod,
            EndPeriod = schedule.EndPeriod,
            StartWeek = schedule.StartWeek,
            EndWeek = schedule.EndWeek,
            WeekParity = schedule.WeekParity,
            Location = schedule.Location,
        };
    }
    /// <inheritdoc />
    public async Task<OperationResult> AddCourseCheckedAsync(CourseDetail course, CancellationToken cancellationToken = default)
    {
        var name = course.CourseName?.Trim() ?? string.Empty;
        var teacher = course.Teacher?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return OperationResult.Fail("课程名不能为空。");
        }

        var slots = course.Schedules.Count > 0
            ? course.Schedules
            : new List<CourseScheduleDetail> { new() { DayOfWeek = course.DayOfWeek, StartPeriod = course.StartPeriod, EndPeriod = course.EndPeriod, StartWeek = course.StartWeek, EndWeek = course.EndWeek, Location = course.Location } };

        if (slots.Count == 0 || slots.All(s => s.StartPeriod <= 0))
        {
            return OperationResult.Fail("至少需要一条时段（星期 / 起止节次 / 起止周）。");
        }

        using var ctx = _factory.CreateDbContext();
        var wanted = slots.Select(s => SlotKeyOf(s.DayOfWeek, s.StartPeriod, s.EndPeriod, s.StartWeek, s.EndWeek, s.WeekParity)).ToHashSet(StringComparer.Ordinal);
        var candidates = await ctx.Courses
            .AsNoTracking()
            .Include(c => c.CourseSchedules)
            .Where(c => c.CourseName == name && c.Teacher == teacher)
            .ToListAsync(cancellationToken);

        var duplicate = candidates.Any(c => c.CourseSchedules
            .Select(s => SlotKeyOf(s.DayOfWeek, s.StartPeriod, s.EndPeriod, s.StartWeek, s.EndWeek, s.WeekParity))
            .ToHashSet(StringComparer.Ordinal)
            .SetEquals(wanted));
        if (duplicate)
        {
            return OperationResult.Fail($"已存在同名同师同时段的课程「{name}（{teacher}）」，未重复新建。");
        }

        course.CourseName = name;
        course.Teacher = teacher;
        course.Schedules = slots;
        return await AddCourseAsync(course, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CourseDeleteImpact> GetDeleteImpactAsync(int courseId, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return new CourseDeleteImpact
        {
            StudentCount = await ctx.StudentCourses.CountAsync(sc => sc.CourseId == courseId, cancellationToken),
            AttendanceCount = await ctx.Attendances.CountAsync(a => a.CourseId == courseId, cancellationToken),
            AdjustmentCount = await ctx.ScheduleAdjustments.CountAsync(a => a.CourseId == courseId, cancellationToken),
        };
    }

    /// <inheritdoc />
    public async Task<OperationResult> DeleteCourseAsync(int courseId, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();
        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var course = await ctx.Courses
                .Include(c => c.CourseSchedules)
                .FirstOrDefaultAsync(c => c.Id == courseId, cancellationToken);
            if (course is null)
            {
                return OperationResult.Fail("课程不存在或已被删除。");
            }

            ctx.CourseSchedules.RemoveRange(course.CourseSchedules);
            ctx.StudentCourses.RemoveRange(ctx.StudentCourses.Where(sc => sc.CourseId == courseId));
            ctx.ScheduleAdjustments.RemoveRange(ctx.ScheduleAdjustments.Where(a => a.CourseId == courseId));
            ctx.Courses.Remove(course);

            await ctx.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"删除课程失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult> ClearStudentScheduleAsync(int studentId, string semester, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(semester))
        {
            return OperationResult.Fail("学期不能为空。");
        }

        using var ctx = _factory.CreateDbContext();
        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);

        ctx.StudentCourses.RemoveRange(ctx.StudentCourses.Where(sc => sc.StudentId == studentId && sc.Semester == semester));
        await ctx.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok();
    }

    /// <inheritdoc />
    public async Task<OperationResult> ClearAllSchedulesAsync(CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();
        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            ctx.StudentCourses.RemoveRange(ctx.StudentCourses);
            ctx.ScheduleAdjustments.RemoveRange(ctx.ScheduleAdjustments);
            ctx.CourseSchedules.RemoveRange(ctx.CourseSchedules);
            ctx.Courses.RemoveRange(ctx.Courses);

            await ctx.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"清空全部课表失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <summary>时段归一化键：星期|起止节次|起止周|单双周（与课表导入的开课班判定同一口径）。</summary>
    private static string SlotKeyOf(int day, int startPeriod, int endPeriod, int startWeek, int endWeek, int parity)
        => $"{day}|{startPeriod}-{endPeriod}|{startWeek}-{endWeek}|{parity}";
    /// <inheritdoc />
    public async Task<ScheduleClearImpact> GetClearAllImpactAsync(CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return new ScheduleClearImpact
        {
            EnrollmentCount = await ctx.StudentCourses.CountAsync(cancellationToken),
            CourseCount = await ctx.Courses.CountAsync(cancellationToken),
            ScheduleSlotCount = await ctx.CourseSchedules.CountAsync(cancellationToken),
            AdjustmentCount = await ctx.ScheduleAdjustments.CountAsync(cancellationToken),
            StudentCount = await ctx.Students.CountAsync(cancellationToken),
            AttendanceCount = await ctx.Attendances.CountAsync(cancellationToken),
        };
    }
}