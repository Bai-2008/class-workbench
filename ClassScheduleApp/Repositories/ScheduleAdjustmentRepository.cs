using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 调休补课调整的数据访问实现。每个方法内创建并释放短生命周期 DbContext。
/// </summary>
public sealed class ScheduleAdjustmentRepository : IScheduleAdjustmentRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ScheduleAdjustmentRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScheduleAdjustment>> GetByDateRangeAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await ctx.ScheduleAdjustments
            .AsNoTracking()
            .Where(a => a.AffectedDate >= start && a.AffectedDate <= end)
            .OrderBy(a => a.AffectedDate)
            .ThenBy(a => a.AdjustmentType)
            .ThenBy(a => a.StartPeriod)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScheduleAdjustment>> GetBySemesterAsync(
        string semester,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await ctx.ScheduleAdjustments
            .AsNoTracking()
            .Where(a => a.Semester == semester)
            .OrderBy(a => a.AffectedDate)
            .ThenBy(a => a.AdjustmentType)
            .ThenBy(a => a.StartPeriod)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScheduleAdjustment>> GetEffectiveChangesAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await ctx.ScheduleAdjustments
            .AsNoTracking()
            .Where(a => a.AffectedDate == date)
            .OrderBy(a => a.AdjustmentType)
            .ThenBy(a => a.StartPeriod)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult> AddAsync(ScheduleAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        return await AddRangeAsync(new[] { adjustment }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult> AddRangeAsync(
        IReadOnlyList<ScheduleAdjustment> adjustments,
        CancellationToken cancellationToken = default)
    {
        if (adjustments.Count == 0)
        {
            return OperationResult.Fail("没有可保存的调整。");
        }

        using var ctx = _factory.CreateDbContext();
        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var adjustment in adjustments)
            {
                if (adjustment.CreatedAt == default)
                {
                    adjustment.CreatedAt = DateTime.Now;
                }

                ctx.ScheduleAdjustments.Add(adjustment);
            }

            await ctx.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail($"保存调整失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult> UpdateAsync(ScheduleAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();
        try
        {
            var entity = await ctx.ScheduleAdjustments.FirstOrDefaultAsync(a => a.Id == adjustment.Id, cancellationToken);
            if (entity is null)
            {
                return OperationResult.Fail("该调整已不存在，请刷新后重试。");
            }

            entity.Semester = adjustment.Semester;
            entity.AdjustmentType = adjustment.AdjustmentType;
            entity.CourseId = adjustment.CourseId;
            entity.AffectedDate = adjustment.AffectedDate;
            entity.StartPeriod = adjustment.StartPeriod;
            entity.EndPeriod = adjustment.EndPeriod;
            entity.FollowWeekday = adjustment.FollowWeekday;
            entity.NewLocation = adjustment.NewLocation;
            entity.Note = adjustment.Note;

            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"更新调整失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();
        try
        {
            var entity = await ctx.ScheduleAdjustments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
            if (entity is null)
            {
                return OperationResult.Fail("该调整已不存在，请刷新后重试。");
            }

            ctx.ScheduleAdjustments.Remove(entity);
            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"删除调整失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SemesterCourseOption>> GetCoursesBySemesterAsync(
        string semester,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var rows = await (
            from sc in ctx.StudentCourses.AsNoTracking()
            join c in ctx.Courses.AsNoTracking() on sc.CourseId equals c.Id
            where sc.Semester == semester
            select new { c.Id, c.CourseNo, c.CourseName, c.Teacher })
            .Distinct()
            .ToListAsync(cancellationToken);

        var options = new List<SemesterCourseOption>();
        foreach (var row in rows)
        {
            options.Add(new SemesterCourseOption(row.Id, row.CourseNo ?? string.Empty, row.CourseName, row.Teacher));
        }

        options.Sort(static (left, right) => string.Compare(left.CourseName, right.CourseName, StringComparison.Ordinal));
        return options;
    }
}
