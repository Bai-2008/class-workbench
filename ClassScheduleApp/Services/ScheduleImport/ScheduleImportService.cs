using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services.ScheduleImport;

/// <summary>
/// 课表导入服务实现：本地确定性解析（<see cref="IScheduleTextParser"/>）+ 覆盖式落库
/// （<see cref="IScheduleImportRepository"/>）。覆盖前调用既有备份服务。
/// </summary>
public sealed class ScheduleImportService : IScheduleImportService
{
    private readonly IScheduleFileReader _fileReader;
    private readonly IScheduleTextParser _parser;
    private readonly IScheduleImportRepository _repository;
    private readonly IBackupService? _backupService;

    /// <inheritdoc />
    public event EventHandler? Imported;

    public ScheduleImportService(
        IScheduleFileReader fileReader,
        IScheduleTextParser parser,
        IScheduleImportRepository repository,
        IBackupService? backupService = null)
    {
        _fileReader = fileReader;
        _parser = parser;
        _repository = repository;
        _backupService = backupService;
    }

    /// <inheritdoc />
    public async Task<ImportFilePreview> PreviewFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var preview = new ImportFilePreview
        {
            FilePath = filePath,
            FileName = System.IO.Path.GetFileName(filePath),
        };

        string content;
        try
        {
            content = _fileReader.ReadText(filePath);
        }
        catch (Exception ex)
        {
            preview.FailureReason = $"读取失败：{ex.Message}";
            return preview;
        }

        return await FillAsync(preview, content, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ImportFilePreview> PreviewTextAsync(string content, string sourceName, CancellationToken cancellationToken = default)
    {
        var preview = new ImportFilePreview { FileName = sourceName };
        return await FillAsync(preview, content, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ScheduleImportReport> ImportAsync(
        IReadOnlyList<(ImportFilePreview Preview, int StudentId)> items,
        string semester,
        CancellationToken cancellationToken = default)
    {
        var report = new ScheduleImportReport();

        if (items.Count == 0)
        {
            report.BackupNote = "没有待导入项";
            return report;
        }

        // 覆盖前自动备份（备份模块未落地则整体跳过并注明）
        if (_backupService is null)
        {
            report.BackupNote = "备份模块未注册，已整体跳过备份";
        }
        else
        {
            try
            {
                report.BackupPath = _backupService.Backup(BackupReasons.PreOp);
            }
            catch (Exception ex)
            {
                report.BackupNote = $"备份失败，已中止导入以避免无备份覆盖：{ex.Message}";
                report.FailedFileCount = items.Count;
                report.Failures.Add($"全部文件未导入：{report.BackupNote}");
                return report;
            }
        }

        // 逐文件执行：每项独立事务，中途失败不回滚已完成的项
        foreach (var (preview, studentId) in items)
        {
            foreach (var skip in preview.Parsed.Skipped)
            {
                report.SkippedLines.Add($"{preview.FileName}：{skip.Reason} :: {Trim(skip.RawLine)}");
            }

            if (!preview.IsUsable)
            {
                report.FailedFileCount++;
                report.Failures.Add($"{preview.FileName}：{preview.FailureReason ?? preview.Parsed.FailureReason ?? "没有可导入的课程行"}");
                continue;
            }

            var activeEntries = preview.Parsed.Entries.Where(e => !e.IsExcluded).ToList();
            if (activeEntries.Count == 0)
            {
                report.FailedFileCount++;
                report.Failures.Add($"{preview.FileName}：全部行已被剔除，没有可导入内容");
                continue;
            }

            try
            {
                var outcome = await _repository.ApplyStudentScheduleAsync(studentId, semester, activeEntries, cancellationToken);
                report.SuccessCount++;
                report.CreatedCourses.AddRange(outcome.CreatedCourses);
                report.LinkedExistingCount += outcome.LinkedExistingCount;
                report.DeletedCourses.AddRange(outcome.DeletedOrphanCourses);
                report.CourseSplits.AddRange(outcome.CourseSplits.Select(d => $"{preview.FileName}：{d}"));
            }
            catch (Exception ex)
            {
                report.FailedFileCount++;
                report.Failures.Add($"{preview.FileName}：导入失败（{ex.Message}）");
            }
        }

        // 有文件真正写入才广播：课表页 / 空闲统计据此即时刷新
        if (report.SuccessCount > 0)
        {
            Imported?.Invoke(this, EventArgs.Empty);
        }

        return report;
    }

    private async Task<ImportFilePreview> FillAsync(ImportFilePreview preview, string content, CancellationToken cancellationToken)
    {
        preview.Parsed = _parser.Parse(content);

        if (!preview.Parsed.IsRecognized)
        {
            preview.FailureReason = preview.Parsed.FailureReason ?? "无法识别的格式";
            return preview;
        }

        await MatchStudentAsync(preview, cancellationToken);
        return preview;
    }

    private async Task MatchStudentAsync(ImportFilePreview preview, CancellationToken cancellationToken)
    {
        // 1) 学号优先（样本头部含学号，最可靠）
        if (!string.IsNullOrWhiteSpace(preview.Parsed.Header.StudentNo))
        {
            var byNo = await _repository.GetStudentByNoAsync(preview.Parsed.Header.StudentNo!, cancellationToken);
            if (byNo is not null)
            {
                preview.MatchState = ImportMatchState.Matched;
                preview.Candidates = new List<Student> { byNo };
                preview.BoundStudent = byNo;
                return;
            }
        }

        // 2) 姓名匹配：唯一则自动绑定，重名则待人工选择
        if (!string.IsNullOrWhiteSpace(preview.Parsed.Header.StudentName))
        {
            var byName = await _repository.FindStudentsByNameAsync(preview.Parsed.Header.StudentName!, cancellationToken);
            if (byName.Count == 1)
            {
                preview.MatchState = ImportMatchState.Matched;
                preview.Candidates = byName.ToList();
                preview.BoundStudent = byName[0];
                return;
            }

            if (byName.Count > 1)
            {
                preview.MatchState = ImportMatchState.Ambiguous;
                preview.Candidates = byName.ToList();
                return;
            }
        }

        // 3) 未匹配：给全部学生供人工绑定
        preview.MatchState = ImportMatchState.NotFound;
        preview.Candidates = (await _repository.GetAllStudentsAsync(cancellationToken)).ToList();
        preview.BoundStudent = null;
    }

    private static string Trim(string text)
    {
        var value = text.Trim();
        return value.Length <= 80 ? value : value[..80] + "…";
    }
}
