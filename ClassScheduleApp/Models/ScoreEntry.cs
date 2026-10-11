namespace ClassScheduleApp.Models;

/// <summary>
/// 综测记录实体，对应 ScoreEntries 表（一表一类）。
/// 分数可为负（扣分）；本表不设唯一约束。
/// 学生删除策略：外键 SET NULL + <see cref="StudentName"/> 名称快照，删除学生后历史综测仍可读（与考勤 CourseName 同语义）。
/// </summary>
public class ScoreEntry
{
    /// <summary>主键。</summary>
    public int Id { get; set; }

    /// <summary>
    /// 关联学生的外键；删除学生时置空（SET NULL），历史记录保留。
    /// 为 null 表示该学生已被删除，此时显示名读 <see cref="StudentName"/> 快照。
    /// </summary>
    public int? StudentId { get; set; }

    /// <summary>关联学生导航属性。</summary>
    public Student? Student { get; set; }

    /// <summary>
    /// 综测发生时的学生姓名快照。
    /// 学生被删除后历史综测仍能显示姓名；录入路径写入，删除学生前对该生缺失快照兜底回填。
    /// </summary>
    public string? StudentName { get; set; }

    /// <summary>学期，取值与 StudentCourses.Semester 一致。</summary>
    public string Semester { get; set; } = string.Empty;

    /// <summary>类别：预设类别或自定义输入。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>分数，可为负表示扣分。</summary>
    public decimal Score { get; set; }

    /// <summary>说明，必填。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>佐证说明，可空。</summary>
    public string? Evidence { get; set; }

    /// <summary>记录日期。</summary>
    public DateOnly EntryDate { get; set; }

    /// <summary>经办人。</summary>
    public string Operator { get; set; } = string.Empty;

    /// <summary>记录创建时间（本地时间）。</summary>
    public DateTime CreatedAt { get; set; }
}
