using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 学期设置的数据访问实现。每个方法内创建并释放短生命周期 DbContext。
/// </summary>
public sealed class SemesterSettingRepository : ISemesterSettingRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SemesterSettingRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SemesterSetting>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await ctx.SemesterSettings
            .AsNoTracking()
            .OrderBy(s => s.SemesterName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SemesterSetting?> GetAsync(string semesterName, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await ctx.SemesterSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SemesterName == semesterName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult> SaveAsync(
        string semesterName,
        DateOnly? startDate,
        int weekCount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(semesterName))
        {
            return OperationResult.Fail("学期不能为空。");
        }

        using var ctx = _factory.CreateDbContext();
        try
        {
            var entity = await ctx.SemesterSettings
                .FirstOrDefaultAsync(s => s.SemesterName == semesterName, cancellationToken);

            if (entity is null)
            {
                ctx.SemesterSettings.Add(new SemesterSetting
                {
                    SemesterName = semesterName,
                    StartDate = startDate,
                    WeekCount = weekCount,
                });
            }
            else
            {
                entity.StartDate = startDate;
                entity.WeekCount = weekCount;
            }

            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"保存学期设置失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<SemesterUsage> GetUsageAsync(string semesterName, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return new SemesterUsage
        {
            EnrollmentCount = await ctx.StudentCourses.CountAsync(sc => sc.Semester == semesterName, cancellationToken),
            AdjustmentCount = await ctx.ScheduleAdjustments.CountAsync(a => a.Semester == semesterName, cancellationToken),
        };
    }

    /// <inheritdoc />
    public async Task<OperationResult> DeleteAsync(
        string semesterName,
        bool includeScheduleData,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(semesterName))
        {
            return OperationResult.Fail("学期不能为空。");
        }

        using var ctx = _factory.CreateDbContext();
        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);

        var usage = new SemesterUsage
        {
            EnrollmentCount = await ctx.StudentCourses.CountAsync(sc => sc.Semester == semesterName, cancellationToken),
            AdjustmentCount = await ctx.ScheduleAdjustments.CountAsync(a => a.Semester == semesterName, cancellationToken),
        };

        if (usage.HasData && !includeScheduleData)
        {
            return OperationResult.Fail(
                $"该学期还有 {usage.EnrollmentCount} 条学生选课、{usage.AdjustmentCount} 条调休补课记录，未删除。" +
                "如需连同这些数据一起删除，请再点一次「删除本学期」确认。");
        }

        if (includeScheduleData)
        {
            ctx.StudentCourses.RemoveRange(ctx.StudentCourses.Where(sc => sc.Semester == semesterName));
            ctx.ScheduleAdjustments.RemoveRange(ctx.ScheduleAdjustments.Where(a => a.Semester == semesterName));
        }

        var entity = await ctx.SemesterSettings.FirstOrDefaultAsync(s => s.SemesterName == semesterName, cancellationToken);
        if (entity is not null)
        {
            ctx.SemesterSettings.Remove(entity);
        }

        await ctx.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return OperationResult.Ok();
    }
}
