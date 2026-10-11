using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 选课关系的数据访问实现。
/// </summary>
public sealed class EnrollmentRepository : IEnrollmentRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public EnrollmentRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>> GetEnrolledCourseIdsAsync(int studentId, string semester, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await ctx.StudentCourses
            .AsNoTracking()
            .Where(sc => sc.StudentId == studentId && sc.Semester == semester)
            .Select(sc => sc.CourseId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveEnrollmentAsync(int studentId, IReadOnlyCollection<int> selectedCourseIds, string semester, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var current = await ctx.StudentCourses
            .Where(sc => sc.StudentId == studentId && sc.Semester == semester)
            .ToListAsync(cancellationToken);

        var selected = new HashSet<int>(selectedCourseIds);

        // 1) 取消勾选：删除已有记录
        var existing = new HashSet<int>();
        var removedCount = 0;
        foreach (var item in current)
        {
            existing.Add(item.CourseId);
            if (!selected.Contains(item.CourseId))
            {
                ctx.StudentCourses.Remove(item);
                removedCount++;
            }
        }

        // 2) 新勾选：插入缺失的记录（已存在的直接跳过，避免触发唯一约束）
        var addedCount = 0;
        foreach (var courseId in selectedCourseIds)
        {
            if (existing.Contains(courseId))
            {
                continue;
            }

            ctx.StudentCourses.Add(new StudentCourse
            {
                StudentId = studentId,
                CourseId = courseId,
                Semester = semester,
            });

            existing.Add(courseId);
            addedCount++;
        }

        if (removedCount == 0 && addedCount == 0)
        {
            return OperationResult.Ok();
        }

        try
        {
            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex) when (DbErrorInspector.IsUniqueConstraintViolation(ex))
        {
            // 理论上已在上面跳过重复项，这里兜底处理并发/脏数据导致的唯一约束冲突
            return OperationResult.Fail(
                "保存失败：该生在本学期已存在相同的选课记录（同一学期同一门课只能选一次）。\n请关闭本窗口后重新打开，再试一次。");
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"保存选课失败：{DbErrorInspector.Describe(ex)}");
        }
    }
}
