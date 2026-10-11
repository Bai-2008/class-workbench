using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 考勤管理的数据访问实现。每个方法内创建并释放短生命周期 DbContext。
/// </summary>
public sealed class AttendanceRepository : IAttendanceRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public AttendanceRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Models.Student>> GetCourseStudentsAsync(
        int courseId,
        string semester,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await ctx.StudentCourses
            .AsNoTracking()
            .Where(sc => sc.CourseId == courseId && sc.Semester == semester)
            .Select(sc => sc.Student!)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.StudentNo)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CourseSlotRow>> GetSemesterSlotsAsync(
        string semester,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await (
                from sc in ctx.StudentCourses.AsNoTracking()
                where sc.Semester == semester
                join cs in ctx.CourseSchedules.AsNoTracking() on sc.CourseId equals cs.CourseId
                join c in ctx.Courses.AsNoTracking() on cs.CourseId equals c.Id
                select new CourseSlotRow
                {
                    CourseId = c.Id,
                    CourseNo = c.CourseNo ?? string.Empty,
                    CourseName = c.CourseName,
                    Teacher = c.Teacher,
                    Location = cs.Location,
                    DayOfWeek = cs.DayOfWeek,
                    StartPeriod = cs.StartPeriod,
                    EndPeriod = cs.EndPeriod,
                    StartWeek = cs.StartWeek,
                    EndWeek = cs.EndWeek,
                    WeekParity = cs.WeekParity,
                })
            .Distinct()
            .OrderBy(row => row.CourseName)
            .ThenBy(row => row.DayOfWeek)
            .ThenBy(row => row.StartPeriod)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttendanceRecordInfo>> GetByRangeAsync(
        DateOnly start,
        DateOnly end,
        int? courseId = null,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var query = ctx.Attendances
            .AsNoTracking()
            .Where(a => a.Date >= start && a.Date <= end);

        if (courseId.HasValue)
        {
            var filterCourseId = courseId.Value;
            query = query.Where(a => a.CourseId == filterCourseId);
        }

        var rows = await (
                from a in query
                join s0 in ctx.Students.AsNoTracking() on a.StudentId equals s0.Id into studentGroup
                from s in studentGroup.DefaultIfEmpty()
                orderby a.Date, s.StudentNo
                select new
                {
                    a.Id,
                    a.StudentId,
                    StudentNo = s.StudentNo,
                    StudentName = s.Name,
                    SnapshotStudentName = a.StudentName,
                    a.Date,
                    a.CourseId,
                    a.CourseName,
                    a.Status,
                    a.Note,
                })
            .ToListAsync(cancellationToken);

        // 课程名单独取，班级活动（CourseId 为 null）不会被牵连
        var courseNames = new Dictionary<int, string>();
        foreach (var course in await ctx.Courses.AsNoTracking().Select(c => new { c.Id, c.CourseName }).ToListAsync(cancellationToken))
        {
            courseNames[course.Id] = course.CourseName;
        }

        var result = new List<AttendanceRecordInfo>();
        foreach (var row in rows)
        {
            var info = new AttendanceRecordInfo
            {
                Id = row.Id,
                StudentId = row.StudentId,
                StudentNo = row.StudentNo ?? string.Empty,
                // 学生已删除（StudentId 为 null / 查不到学生）时，姓名读快照
                StudentName = !string.IsNullOrEmpty(row.SnapshotStudentName)
                    ? row.SnapshotStudentName!
                    : row.StudentName ?? string.Empty,
                Date = row.Date,
                CourseId = row.CourseId,
                Status = row.Status,
                Note = row.Note,
            };

            if (!string.IsNullOrEmpty(row.CourseName))
            {
                // 历史查看优先读课程名快照：课程实体删除后仍能显示当时上的是哪门课
                info.CourseName = row.CourseName;
            }
            else if (row.CourseId.HasValue && courseNames.TryGetValue(row.CourseId.Value, out var courseName))
            {
                info.CourseName = courseName;
            }

            result.Add(info);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<OperationResult> UpsertBatchAsync(
        DateOnly date,
        int? courseId,
        IReadOnlyList<AttendanceEntry> entries,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var existingQuery = ctx.Attendances.Where(a => a.Date == date);
            existingQuery = courseId.HasValue
                ? existingQuery.Where(a => a.CourseId == courseId.Value)
                : existingQuery.Where(a => a.CourseId == null);

            var existing = await existingQuery.ToListAsync(cancellationToken);

            var byStudent = new Dictionary<int, Attendance>();
            foreach (var record in existing)
            {
                // 学生已删除的历史记录（StudentId 为 null）不参与回填匹配
                if (record.StudentId is { } existingStudentId)
                {
                    byStudent[existingStudentId] = record;
                }
            }

            var changed = 0;
            foreach (var entry in entries)
            {
                if (byStudent.TryGetValue(entry.StudentId, out var record))
                {
                    var noteChanged = !string.Equals(record.Note ?? string.Empty, entry.Note ?? string.Empty, StringComparison.Ordinal);
                    if (record.Status != entry.Status || noteChanged)
                    {
                        record.Status = entry.Status;
                        record.Note = entry.Note;
                        changed++;
                    }
                }
                else
                {
                    ctx.Attendances.Add(new Attendance
                    {
                        StudentId = entry.StudentId,
                        // 学生姓名快照：录入时写入，学生被删除后历史考勤仍可读
                        StudentName = ctx.Students.Where(s => s.Id == entry.StudentId).Select(s => s.Name).FirstOrDefault(),
                        Date = date,
                        CourseId = courseId,
                        CourseName = courseId.HasValue
                            ? ctx.Courses.Where(c => c.Id == courseId.Value).Select(c => c.CourseName).FirstOrDefault()
                            : null,
                        Status = entry.Status,
                        Note = entry.Note,
                        CreatedAt = DateTime.Now,
                    });
                    changed++;
                }
            }

            if (changed > 0)
            {
                await ctx.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail($"保存考勤失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentAttendanceStats>> GetStudentStatsAsync(
        DateOnly start,
        DateOnly end,
        int? courseId = null,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var query = ctx.Attendances
            .AsNoTracking()
            .Where(a => a.Date >= start && a.Date <= end);

        if (courseId.HasValue)
        {
            var filterCourseId = courseId.Value;
            query = query.Where(a => a.CourseId == filterCourseId);
        }

        var records = await query
            .Select(a => new { a.StudentId, a.Status })
            .ToListAsync(cancellationToken);

        var students = await ctx.Students
            .AsNoTracking()
            .OrderBy(s => s.StudentNo)
            .Select(s => new { s.Id, s.StudentNo, s.Name })
            .ToListAsync(cancellationToken);

        // 统计在内存里做：数据量小，且避免在 SQL 里拼多列条件聚合
        var present = new Dictionary<int, int>();
        var late = new Dictionary<int, int>();
        var absent = new Dictionary<int, int>();
        var leave = new Dictionary<int, int>();

        foreach (var record in records)
        {
            var bucket = record.Status switch
            {
                AttendanceStatus.Present => present,
                AttendanceStatus.Late => late,
                AttendanceStatus.Absent => absent,
                AttendanceStatus.Leave => leave,
                _ => null,
            };

            if (bucket is null)
            {
                continue;
            }

            // 学生已删除的记录不进当前学生统计（历史明细仍可查）
            if (record.StudentId is not { } recordStudentId)
            {
                continue;
            }

            bucket.TryGetValue(recordStudentId, out var count);
            bucket[recordStudentId] = count + 1;
        }

        var stats = new List<StudentAttendanceStats>();
        foreach (var student in students)
        {
            stats.Add(new StudentAttendanceStats
            {
                StudentId = student.Id,
                StudentNo = student.StudentNo,
                StudentName = student.Name,
                PresentCount = present.TryGetValue(student.Id, out var p) ? p : 0,
                LateCount = late.TryGetValue(student.Id, out var l) ? l : 0,
                AbsentCount = absent.TryGetValue(student.Id, out var a) ? a : 0,
                LeaveCount = leave.TryGetValue(student.Id, out var v) ? v : 0,
            });
        }

        return stats;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CourseAttendanceStats>> GetCourseStatsAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var records = await ctx.Attendances
            .AsNoTracking()
            .Where(a => a.Date >= start && a.Date <= end)
            .Select(a => new { a.CourseId, a.Status })
            .ToListAsync(cancellationToken);

        var courseNames = new Dictionary<int, string>();
        foreach (var course in await ctx.Courses.AsNoTracking().Select(c => new { c.Id, c.CourseName }).ToListAsync(cancellationToken))
        {
            courseNames[course.Id] = course.CourseName;
        }

        var grouped = new Dictionary<int, CourseAttendanceStats>();
        var classActivityKey = -1;

        foreach (var record in records)
        {
            var key = record.CourseId ?? classActivityKey;
            if (!grouped.TryGetValue(key, out var stats))
            {
                stats = new CourseAttendanceStats
                {
                    CourseId = record.CourseId,
                    OccasionName = record.CourseId.HasValue && courseNames.TryGetValue(record.CourseId.Value, out var name)
                        ? name
                        : "班级活动",
                };
                grouped[key] = stats;
            }

            switch (record.Status)
            {
                case AttendanceStatus.Present:
                    stats.PresentCount++;
                    break;
                case AttendanceStatus.Late:
                    stats.LateCount++;
                    break;
                case AttendanceStatus.Absent:
                    stats.AbsentCount++;
                    break;
                case AttendanceStatus.Leave:
                    stats.LeaveCount++;
                    break;
            }
        }

        var result = new List<CourseAttendanceStats>(grouped.Values);
        result.Sort(static (left, right) => string.CompareOrdinal(left.OccasionName, right.OccasionName));
        return result;
    }
}
