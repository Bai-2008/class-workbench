using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 学生维护的数据访问实现。按规范注入 IDbContextFactory，每个方法内创建并释放 DbContext。
/// </summary>
public sealed class StudentAdminRepository : IStudentAdminRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public StudentAdminRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAllStudentNosAsync(CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await ctx.Students
            .AsNoTracking()
            .Select(s => s.StudentNo)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult> AddRangeAsync(IReadOnlyList<Student> students, CancellationToken cancellationToken = default)
    {
        if (students.Count == 0)
        {
            return OperationResult.Ok();
        }

        using var ctx = _factory.CreateDbContext();
        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            ctx.Students.AddRange(students);
            await ctx.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail("批量新增学生失败：" + DbErrorInspector.Describe(ex));
        }
    }

    /// <inheritdoc />
    public async Task<StudentDeleteImpact> GetDeleteRangeImpactAsync(IReadOnlyList<int> studentIds, CancellationToken cancellationToken = default)
    {
        if (studentIds.Count == 0)
        {
            return new StudentDeleteImpact();
        }

        using var ctx = _factory.CreateDbContext();

        return new StudentDeleteImpact
        {
            StudentCount = await ctx.Students.CountAsync(s => studentIds.Contains(s.Id), cancellationToken),
            EnrollmentCount = await ctx.StudentCourses.CountAsync(sc => studentIds.Contains(sc.StudentId), cancellationToken),
            AttendanceCount = await ctx.Attendances.CountAsync(a => a.StudentId != null && studentIds.Contains(a.StudentId.Value), cancellationToken),
            ScoreEntryCount = await ctx.ScoreEntries.CountAsync(e => e.StudentId != null && studentIds.Contains(e.StudentId.Value), cancellationToken),
        };
    }

    /// <inheritdoc />
    public async Task<OperationResult> DeleteRangeAsync(IReadOnlyList<int> studentIds, CancellationToken cancellationToken = default)
    {
        if (studentIds.Count == 0)
        {
            return OperationResult.Ok();
        }

        using var ctx = _factory.CreateDbContext();
        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var students = await ctx.Students
                .Where(s => studentIds.Contains(s.Id))
                .ToListAsync(cancellationToken);

            if (students.Count == 0)
            {
                return OperationResult.Fail("所选学生不存在或已被删除。");
            }

            var nameById = new Dictionary<int, string>();
            foreach (var student in students)
            {
                nameById[student.Id] = student.Name;
            }

            // 兜底回填：删除前把该批学生缺失的姓名快照补齐，保证历史记录仍能显示姓名
            foreach (var attendance in await ctx.Attendances
                         .Where(a => a.StudentId != null && studentIds.Contains(a.StudentId.Value))
                         .ToListAsync(cancellationToken))
            {
                if (string.IsNullOrEmpty(attendance.StudentName) && nameById.TryGetValue(attendance.StudentId!.Value, out var attendanceName))
                {
                    attendance.StudentName = attendanceName;
                }
            }

            foreach (var entry in await ctx.ScoreEntries
                         .Where(e => e.StudentId != null && studentIds.Contains(e.StudentId.Value))
                         .ToListAsync(cancellationToken))
            {
                if (string.IsNullOrEmpty(entry.StudentName) && nameById.TryGetValue(entry.StudentId!.Value, out var entryName))
                {
                    entry.StudentName = entryName;
                }
            }

            await ctx.SaveChangesAsync(cancellationToken);

            // 删除学生：选课关联级联删除；考勤与综测外键 SET NULL（记录保留）
            ctx.Students.RemoveRange(students);
            await ctx.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail("批量删除学生失败：" + DbErrorInspector.Describe(ex));
        }
    }

    public async Task<bool> IsStudentNoTakenAsync(string studentNo, int? excludeStudentId = null, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var query = ctx.Students.AsNoTracking().Where(s => s.StudentNo == studentNo);
        if (excludeStudentId.HasValue)
        {
            var excludeId = excludeStudentId.Value;
            query = query.Where(s => s.Id != excludeId);
        }

        return await query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult> AddStudentAsync(Student student, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.Students.Add(student);

        try
        {
            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex) when (DbErrorInspector.IsUniqueConstraintViolation(ex))
        {
            return OperationResult.Fail($"学号 {student.StudentNo} 已存在，请更换后重试。");
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"保存学生失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult> UpdateStudentAsync(Student student, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.Students.Update(student);

        try
        {
            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex) when (DbErrorInspector.IsUniqueConstraintViolation(ex))
        {
            return OperationResult.Fail($"学号 {student.StudentNo} 已被其他学生使用，请更换后重试。");
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"保存学生失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult> DeleteStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var student = await ctx.Students.FindAsync(new object[] { studentId }, cancellationToken);
        if (student is null)
        {
            return OperationResult.Fail("该学生已不存在，可能已被其他操作删除。");
        }

        // 只删除学生本身，其选课记录由数据库外键 ON DELETE CASCADE 级联删除
        ctx.Students.Remove(student);

        try
        {
            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"删除学生失败：{DbErrorInspector.Describe(ex)}");
        }
    }
}
