using System.Globalization;
using System.Text;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services;

/// <summary>
/// 综测汇总的纯文本生成：Tab 分隔，列顺序与界面交叉表、导出 Sheet1 完全一致，
/// 保证粘贴进 Excel 时各列对齐。无依赖，供 ViewModel 与自测共用。
/// </summary>
public static class ScoreSummaryText
{
    /// <summary>交叉表表头列（姓名 + 学号 + 各预设类别 + 总分）。</summary>
    public static IReadOnlyList<string> BuildHeaders()
    {
        var headers = new List<string> { "姓名", "学号" };
        headers.AddRange(ScoreCategories.Presets);
        headers.Add("总分");
        return headers;
    }

    /// <summary>生成 Tab 分隔文本（首行为表头，其后每名学生一行）。</summary>
    public static string BuildTabSeparated(IReadOnlyList<StudentScoreSummary> summaries)
    {
        var builder = new StringBuilder();
        builder.Append(string.Join('\t', BuildHeaders()));
        builder.Append("\r\n");

        foreach (var summary in summaries)
        {
            var cells = new List<string>
            {
                summary.StudentName,
                summary.StudentNo,
            };

            foreach (var preset in ScoreCategories.Presets)
            {
                var value = summary.CategoryScores.TryGetValue(preset, out var score) ? score : 0m;
                cells.Add(value.ToString("0.##", CultureInfo.InvariantCulture));
            }

            cells.Add(summary.TotalScore.ToString("0.##", CultureInfo.InvariantCulture));
            builder.Append(string.Join('\t', cells));
            builder.Append("\r\n");
        }

        return builder.ToString();
    }
}
