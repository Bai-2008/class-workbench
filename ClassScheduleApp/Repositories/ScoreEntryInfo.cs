using System.Globalization;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 综测记录明细（查询结果）：已把学生信息组装好，供界面与导出直接使用。
/// </summary>
public sealed class ScoreEntryInfo
{
    /// <summary>记录 Id。</summary>
    public int Id { get; set; }

    /// <summary>学生 Id；学生被删除后为 null（此时姓名读快照）。</summary>
    public int? StudentId { get; set; }

    /// <summary>学号。</summary>
    public string StudentNo { get; set; } = string.Empty;

    /// <summary>学生姓名。</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>学期。</summary>
    public string Semester { get; set; } = string.Empty;

    /// <summary>类别。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>分数（可为负）。</summary>
    public decimal Score { get; set; }

    /// <summary>说明。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>佐证说明。</summary>
    public string? Evidence { get; set; }

    /// <summary>记录日期。</summary>
    public DateOnly EntryDate { get; set; }

    /// <summary>是否扣分（负数）。</summary>
    public bool IsNegative => Score < 0m;

    /// <summary>日期展示文本。</summary>
    public string EntryDateText => EntryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>带正负号的分数展示文本。</summary>
    public string ScoreText => (Score > 0m ? "+" : string.Empty) + Score.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>学生展示文本（姓名（学号））；学生已删除时只显示姓名快照。</summary>
    public string StudentText => StudentNo.Length == 0 ? StudentName : $"{StudentName}（{StudentNo}）";
}
