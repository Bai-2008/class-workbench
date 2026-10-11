using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 作息时间的数据访问实现。每个方法内创建并释放短生命周期 DbContext。
/// </summary>
public sealed class PeriodTimeRepository : IPeriodTimeRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PeriodTimeRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PeriodTime>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await ctx.PeriodTimes
            .AsNoTracking()
            .OrderBy(p => p.PeriodNo)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult> UpdateAllAsync(
        IReadOnlyList<PeriodTime> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return OperationResult.Fail("没有可保存的作息时间。");
        }

        using var ctx = _factory.CreateDbContext();
        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var existing = await ctx.PeriodTimes.ToListAsync(cancellationToken);
            var byPeriod = new Dictionary<int, PeriodTime>();
            foreach (var entity in existing)
            {
                byPeriod[entity.PeriodNo] = entity;
            }

            var kept = new HashSet<int>();
            foreach (var item in items)
            {
                kept.Add(item.PeriodNo);

                if (byPeriod.TryGetValue(item.PeriodNo, out var entity))
                {
                    entity.StartTime = item.StartTime;
                    entity.EndTime = item.EndTime;
                }
                else
                {
                    ctx.PeriodTimes.Add(new PeriodTime
                    {
                        PeriodNo = item.PeriodNo,
                        StartTime = item.StartTime,
                        EndTime = item.EndTime,
                    });
                }
            }

            // 节数调小：删掉清单之外的节次（整体替换语义，与课程多时段一致）
            foreach (var entity in existing)
            {
                if (!kept.Contains(entity.PeriodNo))
                {
                    ctx.PeriodTimes.Remove(entity);
                }
            }

            await ctx.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail($"保存作息时间失败：{DbErrorInspector.Describe(ex)}");
        }
    }
}
