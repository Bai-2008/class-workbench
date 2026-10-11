using System.Globalization;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 单个学生在某学期的综测汇总：各预设类别的得分合计 + 总分（含自定义类别）。
/// </summary>
public sealed class StudentScoreSummary
{
    /// <summary>学生 Id。</summary>
    public int StudentId { get; set; }

    /// <summary>学号。</summary>
    public string StudentNo { get; set; } = string.Empty;

    /// <summary>学生姓名。</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>
    /// 各预设类别的得分合计（所有预设键都存在，无记录为 0）。
    /// 供交叉表按类别名索引绑定。
    /// </summary>
    public IReadOnlyDictionary<string, decimal> CategoryScores { get; set; } =
        new Dictionary<string, decimal>();

    /// <summary>自定义类别（非预设）的得分合计。</summary>
    public decimal OtherScore { get; set; }

    /// <summary>总分 = 该学期全部记录（含自定义类别）之和。</summary>
    public decimal TotalScore { get; set; }

    /// <summary>总分展示文本。</summary>
    public string TotalScoreText => TotalScore.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>是否总分为负（界面红色显示）。</summary>
    public bool IsTotalNegative => TotalScore < 0m;
}
