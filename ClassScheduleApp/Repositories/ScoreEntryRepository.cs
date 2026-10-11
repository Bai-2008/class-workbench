using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 综测记录的数据访问实现。每个方法内创建并释放短生命周期 DbContext。
/// 分数是 decimal（SQLite 存 TEXT），聚合一律在内存里做，避免 SQL 端精度问题。
/// </summary>
public sealed class ScoreEntryRepository : IScoreEntryRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ScoreEntryRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScoreEntryInfo>> GetEntriesAsync(
        string? semester = null,
        int? studentId = null,
        string? category = null,
        DateOnly? rangeStart = null,
        DateOnly? rangeEnd = null,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var rows = await BuildQuery(ctx, semester, studentId, category, rangeStart, rangeEnd)
            .OrderBy(e => e.EntryDate)
            .ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.StudentId,
                e.Semester,
                e.Category,
                e.Score,
                e.Description,
                e.Evidence,
                e.EntryDate,
                e.StudentName,
            })
            .ToListAsync(cancellationToken);

        var students = await LoadStudentIndexAsync(ctx, cancellationToken);

        var result = new List<ScoreEntryInfo>();
        foreach (var row in rows)
        {
            var info = new ScoreEntryInfo
            {
                Id = row.Id,
                StudentId = row.StudentId,
                Semester = row.Semester,
                Category = row.Category,
                Score = row.Score,
                Description = row.Description,
                Evidence = row.Evidence,
                EntryDate = row.EntryDate,
            };

            // 学生已删除（StudentId 为 null）时，姓名读快照；否则优先用快照、缺失再回退学生表
            info.StudentName = !string.IsNullOrEmpty(row.StudentName) ? row.StudentName! : string.Empty;

            if (row.StudentId is { } rowStudentId2 && students.TryGetValue(rowStudentId2, out var student))
            {
                info.StudentNo = student.StudentNo;
                if (info.StudentName.Length == 0)
                {
                    info.StudentName = student.Name;
                }
            }

            result.Add(info);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<OperationResult> AddBatchAsync(
        IReadOnlyList<int> studentIds,
        ScoreEntryDraft draft,
        CancellationToken cancellationToken = default)
    {
        if (studentIds.Count == 0)
        {
            return OperationResult.Fail("没有选择学生。");
        }

        using var ctx = _factory.CreateDbContext();
        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var studentId in studentIds)
            {
                ctx.ScoreEntries.Add(new ScoreEntry
                {
                    StudentId = studentId,
                    // 姓名快照：录入时写入，学生被删除后历史综测仍可读
                    StudentName = ctx.Students.Where(s => s.Id == studentId).Select(s => s.Name).FirstOrDefault(),
                    Semester = draft.Semester,
                    Category = draft.Category,
                    Score = draft.Score,
                    Description = draft.Description,
                    Evidence = draft.Evidence,
                    EntryDate = draft.EntryDate,
                    Operator = draft.Handler,
                    CreatedAt = DateTime.Now,
                });
            }

            await ctx.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult.Fail($"保存综测记录失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult> UpdateAsync(
        int id,
        ScoreEntryDraft draft,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();
        try
        {
            var entity = await ctx.ScoreEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (entity is null)
            {
                return OperationResult.Fail("要修改的记录不存在，可能已被删除。");
            }

            entity.Semester = draft.Semester;
            entity.Category = draft.Category;
            entity.Score = draft.Score;
            entity.Description = draft.Description;
            entity.Evidence = draft.Evidence;
            entity.EntryDate = draft.EntryDate;
            entity.Operator = draft.Handler;

            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"修改综测记录失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<OperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();
        try
        {
            var entity = await ctx.ScoreEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
            if (entity is null)
            {
                return OperationResult.Fail("要删除的记录不存在，可能已被删除。");
            }

            ctx.ScoreEntries.Remove(entity);
            await ctx.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (DbUpdateException ex)
        {
            return OperationResult.Fail($"删除综测记录失败：{DbErrorInspector.Describe(ex)}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentScoreSummary>> GetSummaryAsync(
        string semester,
        CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var rows = await ctx.ScoreEntries
            .AsNoTracking()
            .Where(e => e.Semester == semester)
            .Select(e => new { e.StudentId, e.Category, e.Score })
            .ToListAsync(cancellationToken);

        var students = await ctx.Students
            .AsNoTracking()
            .OrderBy(s => s.StudentNo)
            .Select(s => new { s.Id, s.StudentNo, s.Name })
            .ToListAsync(cancellationToken);

        // 每学生：预设类别先全部置 0，再累加；非预设类别计入 OtherScore
        var byStudent = new Dictionary<int, StudentScoreSummary>();
        foreach (var student in students)
        {
            var scores = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var preset in ScoreCategories.Presets)
            {
                scores[preset] = 0m;
            }

            byStudent[student.Id] = new StudentScoreSummary
            {
                StudentId = student.Id,
                StudentNo = student.StudentNo,
                StudentName = student.Name,
                CategoryScores = scores,
            };
        }

        foreach (var row in rows)
        {
            // 学生已删除的记录不进当前学生汇总（历史明细仍可查）
            if (row.StudentId is not { } rowStudentId ||
                !byStudent.TryGetValue(rowStudentId, out var summary))
            {
                continue;
            }

            summary.TotalScore += row.Score;

            if (summary.CategoryScores is Dictionary<string, decimal> mutable &&
                mutable.ContainsKey(row.Category))
            {
                mutable[row.Category] += row.Score;
            }
            else
            {
                summary.OtherScore += row.Score;
            }
        }

        var result = new List<StudentScoreSummary>(byStudent.Values);
        result.Sort(static (left, right) =>
        {
            var byTotal = right.TotalScore.CompareTo(left.TotalScore);
            return byTotal != 0 ? byTotal : string.CompareOrdinal(left.StudentNo, right.StudentNo);
        });

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>> FindDuplicateStudentIdsAsync(
        string semester,
        IReadOnlyList<int> studentIds,
        string category,
        decimal score,
        string description,
        CancellationToken cancellationToken = default)
    {
        if (studentIds.Count == 0)
        {
            return Array.Empty<int>();
        }

        using var ctx = _factory.CreateDbContext();

        var rows = await ctx.ScoreEntries
            .AsNoTracking()
            .Where(e => e.Semester == semester && e.Category == category && e.Score == score && e.Description == description && e.StudentId != null)
            .Select(e => e.StudentId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var candidates = new HashSet<int>(studentIds);
        var duplicates = new List<int>();
        foreach (var studentId in rows)
        {
            if (candidates.Contains(studentId))
            {
                duplicates.Add(studentId);
            }
        }

        return duplicates;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        var existing = await ctx.ScoreEntries
            .AsNoTracking()
            .Select(e => e.Category)
            .Distinct()
            .ToListAsync(cancellationToken);

        var result = new List<string>(ScoreCategories.Presets);
        var extras = new List<string>();
        foreach (var category in existing)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                continue;
            }

            var isPreset = false;
            foreach (var preset in ScoreCategories.Presets)
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

    /// <summary>组装同一组过滤条件的查询（明细与重复检查共用）。</summary>
    private static IQueryable<ScoreEntry> BuildQuery(
        AppDbContext ctx,
        string? semester,
        int? studentId,
        string? category,
        DateOnly? rangeStart,
        DateOnly? rangeEnd)
    {
        var query = ctx.ScoreEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(semester))
        {
            var filterSemester = semester.Trim();
            query = query.Where(e => e.Semester == filterSemester);
        }

        if (studentId.HasValue)
        {
            var filterStudentId = studentId.Value;
            query = query.Where(e => e.StudentId == filterStudentId);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var filterCategory = category.Trim();
            query = query.Where(e => e.Category == filterCategory);
        }

        if (rangeStart.HasValue)
        {
            var start = rangeStart.Value;
            query = query.Where(e => e.EntryDate >= start);
        }

        if (rangeEnd.HasValue)
        {
            var end = rangeEnd.Value;
            query = query.Where(e => e.EntryDate <= end);
        }

        return query;
    }

    private static async Task<Dictionary<int, Student>> LoadStudentIndexAsync(
        AppDbContext ctx,
        CancellationToken cancellationToken)
    {
        var students = await ctx.Students.AsNoTracking().ToListAsync(cancellationToken);
        var index = new Dictionary<int, Student>();
        foreach (var student in students)
        {
            index[student.Id] = student;
        }

        return index;
    }
}
