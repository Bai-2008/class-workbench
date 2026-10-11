using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 班费记账的数据访问实现。每个方法内创建并释放短生命周期 DbContext。
/// 金额是 decimal（SQLite 存 TEXT），聚合一律在内存里做，避免 SQL 端精度问题。
/// </summary>
public sealed class ExpenseRepository : IExpenseRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ExpenseRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExpenseRecordInfo>> GetEntriesAsync(
        DateOnly start,
        DateOnly end,
        ExpenseType? type = null,
        string? category = null,
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var rows = await BuildQuery(ctx, start, end, type, category, keyword)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.Date,
                e.Type,
                e.Amount,
                e.Category,
                e.Description,
                e.Handler,
                e.Note,
            })
            .ToListAsync(cancellationToken);

        var result = new List<ExpenseRecordInfo>();
        foreach (var row in rows)
        {
            result.Add(new ExpenseRecordInfo
            {
                Id = row.Id,
                Date = row.Date,
                Type = row.Type,
                Amount = row.Amount,
                Category = row.Category,
                Description = row.Description,
                Handler = row.Handler,
                Note = row.Note,
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<ExpenseSummary> GetSummaryAsync(
        DateOnly start,
        DateOnly end,
        ExpenseType? type = null,
        string? category = null,
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var rows = await BuildQuery(ctx, start, end, type, category, keyword)
            .Select(e => new { e.Type, e.Amount, e.Category })
            .ToListAsync(cancellationToken);

        var summary = new ExpenseSummary();
        var subtotals = new List<ExpenseCategorySubtotal>();
        var byCategory = new Dictionary<string, ExpenseCategorySubtotal>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            summary.EntryCount++;

            if (row.Type == ExpenseType.Income)
            {
                summary.TotalIncome += row.Amount;
            }
            else
            {
                summary.TotalExpense += row.Amount;
            }

            if (!byCategory.TryGetValue(row.Category, out var subtotal))
            {
                subtotal = new ExpenseCategorySubtotal { Category = row.Category };
                byCategory[row.Category] = subtotal;
                subtotals.Add(subtotal);
            }

            subtotal.EntryCount++;
            if (row.Type == ExpenseType.Income)
            {
                subtotal.IncomeAmount += row.Amount;
            }
            else
            {
                subtotal.ExpenseAmount += row.Amount;
            }
        }

        subtotals.Sort(static (left, right) => string.CompareOrdinal(left.Category, right.Category));
        summary.SubTotals = subtotals;
        return summary;
    }

    /// <inheritdoc />
    public async Task<OperationResult> AddAsync(ExpenseInput input, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();
        try
        {
            ctx.Expenses.Add(new Expense
            {
                Date = input.Date,
                Type = input.Type,
                Amount = input.Amount,
                Category = input.Category,
                Description = input.Description,
                Handler = input.Handler,
                Note = input.Note,
                CreatedAt = DateTime.Now,
            });

            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"保存班费流水失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult> UpdateAsync(int id, ExpenseInput input, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();
        try
        {
            var entity = await ctx.Expenses.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (entity is null)
            {
                return OperationResult.Fail("要修改的流水不存在，可能已被删除。");
            }

            entity.Date = input.Date;
            entity.Type = input.Type;
            entity.Amount = input.Amount;
            entity.Category = input.Category;
            entity.Description = input.Description;
            entity.Handler = input.Handler;
            entity.Note = input.Note;

            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"修改班费流水失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();
        try
        {
            var entity = await ctx.Expenses.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (entity is null)
            {
                return OperationResult.Fail("要删除的流水不存在，可能已被删除。");
            }

            ctx.Expenses.Remove(entity);
            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"删除班费流水失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var existing = await ctx.Expenses
            .AsNoTracking()
            .Select(e => e.Category)
            .Distinct()
            .ToListAsync(cancellationToken);

        var result = new List<string>();
        foreach (var preset in ExpenseCategories.Presets)
        {
            result.Add(preset);
        }

        // 历史里出现过的自定义类别追加在后面（预设类别不重复）
        var extras = new List<string>();
        foreach (var category in existing)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                continue;
            }

            var isPreset = false;
            foreach (var preset in ExpenseCategories.Presets)
            {
                if (string.Equals(preset, category, StringComparison.Ordinal))
                {
                    isPreset = true;
                    break;
                }
            }

            if (!isPreset)
            {
                extras.Add(category);
            }
        }

        extras.Sort(static (left, right) => string.CompareOrdinal(left, right));
        result.AddRange(extras);
        return result;
    }

    /// <inheritdoc />
    public async Task<string?> GetLastHandlerAsync(CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var handler = await ctx.Expenses
            .AsNoTracking()
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.Id)
            .Select(e => e.Handler)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(handler) ? null : handler;
    }

    /// <summary>组装同一组过滤条件的查询（明细与汇总共用，保证口径一致）。</summary>
    private static IQueryable<Expense> BuildQuery(
        AppDbContext ctx,
        DateOnly start,
        DateOnly end,
        ExpenseType? type,
        string? category,
        string? keyword)
    {
        var query = ctx.Expenses
            .AsNoTracking()
            .Where(e => e.Date >= start && e.Date <= end);

        if (type.HasValue)
        {
            var filterType = type.Value;
            query = query.Where(e => e.Type == filterType);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var filterCategory = category.Trim();
            query = query.Where(e => e.Category == filterCategory);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var filterKeyword = keyword.Trim();
            query = query.Where(e =>
                e.Description.Contains(filterKeyword) ||
                e.Category.Contains(filterKeyword) ||
                e.Handler.Contains(filterKeyword));
        }

        return query;
    }
}
