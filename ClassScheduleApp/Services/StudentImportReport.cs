namespace ClassScheduleApp.Services;

/// <summary>
/// 批量导入学生的报告：新增 N / 跳过 N（逐行原因）/ 失败 N（逐行原因）。
/// 呈现风格沿用课表导入报告（多行文本）。
/// </summary>
public sealed class StudentImportReport
{
    /// <summary>新增入库人数。</summary>
    public int AddedCount { get; set; }

    /// <summary>跳过人数（已存在 / 缺必填列等）。</summary>
    public int SkippedCount { get; set; }

    /// <summary>失败行数（文件无法解析 / 读取异常等）。</summary>
    public int FailedCount { get; set; }

    /// <summary>新增学生的「学号 姓名」清单。</summary>
    public List<string> Added { get; } = new();

    /// <summary>跳过原因（逐行）。</summary>
    public List<string> Skipped { get; } = new();

    /// <summary>失败原因（逐行或整文件）。</summary>
    public List<string> Failures { get; } = new();

    /// <summary>数据来源说明（文件名与编码/格式）。</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>渲染成多行文本报告。</summary>
    public string ToText()
    {
        var lines = new List<string>
        {
            $"来源：{Source}",
            $"新增 {AddedCount} 人，跳过 {SkippedCount} 行，失败 {FailedCount} 行。",
        };

        if (Added.Count > 0)
        {
            var preview = Added.Count <= 10
                ? string.Join("、", Added)
                : string.Join("、", Added.Take(10)) + $"…（共 {Added.Count} 人）";
            lines.Add("新增名单：" + preview);
        }

        if (Skipped.Count > 0)
        {
            lines.Add("跳过行：");
            lines.AddRange(Skipped.Select(s => "  · " + s));
        }

        if (Failures.Count > 0)
        {
            lines.Add("失败行：");
            lines.AddRange(Failures.Select(f => "  · " + f));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
