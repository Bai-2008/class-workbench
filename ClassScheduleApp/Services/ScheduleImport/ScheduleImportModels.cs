using ClassScheduleApp.Models;

namespace ClassScheduleApp.Services.ScheduleImport;

/// <summary>学生自动匹配的结果状态。</summary>
public enum ImportMatchState
{
    /// <summary>按学号或唯一姓名自动匹配成功。</summary>
    Matched,

    /// <summary>找不到同名/同学号学生，需要人工绑定。</summary>
    NotFound,

    /// <summary>姓名重名（多个候选），需要人工选择。</summary>
    Ambiguous,
}

/// <summary>
/// 单个文件的解析与匹配预览（批量清单与单个导入共用）。
/// </summary>
public sealed class ImportFilePreview
{
    /// <summary>文件完整路径；粘贴文本导入时为空。</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>文件名（粘贴文本时为"粘贴的文本"之类来源名）。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>解析结果。</summary>
    public ParsedSchedule Parsed { get; set; } = new();

    /// <summary>读文件/解析阶段就失败时的原因（此时无法导入）。</summary>
    public string? FailureReason { get; set; }

    /// <summary>匹配状态。</summary>
    public ImportMatchState MatchState { get; set; } = ImportMatchState.NotFound;

    /// <summary>可选学生候选（自动匹配成功时为 1 个）。</summary>
    public List<Student> Candidates { get; set; } = new();

    /// <summary>当前绑定的学生（自动匹配成功时已填好）。</summary>
    public Student? BoundStudent { get; set; }

    /// <summary>该文件是否可用（解析成功且有课程行）。</summary>
    public bool IsUsable => FailureReason is null && Parsed.IsRecognized && Parsed.Entries.Count > 0;

    /// <summary>课程数（可导入的行数）。</summary>
    public int CourseCount => Parsed.Entries.Count;

    /// <summary>异常行数。</summary>
    public int SkipCount => Parsed.Skipped.Count;

    /// <summary>匹配状态展示文本。</summary>
    public string MatchText => MatchState switch
    {
        ImportMatchState.Matched => BoundStudent is null ? "已匹配" : $"{BoundStudent.Name}（{BoundStudent.StudentNo}）",
        ImportMatchState.Ambiguous => "重名待选",
        _ => "未匹配待绑定",
    };
}

/// <summary>
/// 导入报告：成功/失败、课程增删、差异与跳过行、备份路径。
/// </summary>
public sealed class ScheduleImportReport
{
    /// <summary>成功导入的学生数。</summary>
    public int SuccessCount { get; set; }

    /// <summary>失败文件数。</summary>
    public int FailedFileCount { get; set; }

    /// <summary>本次新建的课程。</summary>
    public List<string> CreatedCourses { get; } = new();

    /// <summary>关联到既有课程的次数。</summary>
    public int LinkedExistingCount { get; set; }

    /// <summary>因零关联被删除的课程。</summary>
    public List<string> DeletedCourses { get; } = new();

    /// <summary>开课班拆分：同名同师但时段集合不一致而新建的课程（含双方时段对比）。</summary>
    public List<string> CourseSplits { get; } = new();

    /// <summary>跳过行（含文件与原因）。</summary>
    public List<string> SkippedLines { get; } = new();

    /// <summary>失败文件（含原因）。</summary>
    public List<string> Failures { get; } = new();

    /// <summary>备份目录路径；未执行备份时为 null。</summary>
    public string? BackupPath { get; set; }

    /// <summary>备份说明（未备份/失败原因）。</summary>
    public string? BackupNote { get; set; }

    /// <summary>渲染成多行文本报告。</summary>
    public string ToText()
    {
        var lines = new List<string>
        {
            $"导入完成：成功 {SuccessCount} 人，失败 {FailedFileCount} 个文件。",
            $"新建课程 {CreatedCourses.Count} 门，关联既有课程 {LinkedExistingCount} 门，删除零关联课程 {DeletedCourses.Count} 门。",
            $"开课班拆分 {CourseSplits.Count} 条，跳过 {SkippedLines.Count} 行。",
            BackupPath is null ? $"备份：未执行（{BackupNote ?? "无"}）。" : $"备份：{BackupPath}",
        };

        if (CreatedCourses.Count > 0)
        {
            lines.Add("新建课程：" + string.Join("；", CreatedCourses));
        }

        if (DeletedCourses.Count > 0)
        {
            lines.Add("删除的零关联课程：" + string.Join("；", DeletedCourses));
        }

        if (CourseSplits.Count > 0)
        {
            lines.Add("开课班拆分（同名同师、时段集合不一致，已建为不同开课班）：");
            lines.AddRange(CourseSplits.Select(s => "  · " + s));
        }

        if (SkippedLines.Count > 0)
        {
            lines.Add("跳过行：");
            lines.AddRange(SkippedLines.Select(s => "  · " + s));
        }

        if (Failures.Count > 0)
        {
            lines.Add("失败文件：");
            lines.AddRange(Failures.Select(f => "  · " + f));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
