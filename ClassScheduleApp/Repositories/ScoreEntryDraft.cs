namespace ClassScheduleApp.Repositories;

/// <summary>
/// 综测记录的内容（不含学生）：批量添加时对每个选中的学生插入同样内容，编辑时按此内容覆盖。
/// </summary>
public sealed class ScoreEntryDraft
{
    public ScoreEntryDraft(
        string semester,
        string category,
        decimal score,
        string description,
        string? evidence,
        DateOnly entryDate,
        string handler)
    {
        Semester = semester;
        Category = category;
        Score = score;
        Description = description;
        Evidence = evidence;
        EntryDate = entryDate;
        Handler = handler;
    }

    /// <summary>学期。</summary>
    public string Semester { get; }

    /// <summary>类别。</summary>
    public string Category { get; }

    /// <summary>分数（可为负）。</summary>
    public decimal Score { get; }

    /// <summary>说明。</summary>
    public string Description { get; }

    /// <summary>佐证说明，可空。</summary>
    public string? Evidence { get; }

    /// <summary>记录日期。</summary>
    public DateOnly EntryDate { get; }

    /// <summary>经办人。</summary>
    public string Handler { get; }
}
