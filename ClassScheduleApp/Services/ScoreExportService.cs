using System.IO;
using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using ClosedXML.Excel;

namespace ClassScheduleApp.Services;

/// <summary>
/// 基于 ClosedXML 的综测汇总导出实现。导出失败一律转成 ExportResult.Fail，不影响界面。
/// </summary>
public sealed class ScoreExportService : IScoreExportService
{
    /// <inheritdoc />
    public Task<ExportResult> ExportAsync(
        string semester,
        IReadOnlyList<StudentScoreSummary> summaries,
        IReadOnlyList<ScoreEntryInfo> entries,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = BuildFilePath(semester);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var workbook = new XLWorkbook();

            // Sheet1：汇总交叉表（列顺序与界面、剪贴板文本一致）
            var sheet = workbook.Worksheets.Add("汇总交叉表");
            var headers = ScoreSummaryText.BuildHeaders();
            for (var column = 0; column < headers.Count; column++)
            {
                sheet.Cell(1, column + 1).Value = headers[column];
            }

            var row = 2;
            foreach (var summary in summaries)
            {
                sheet.Cell(row, 1).Value = summary.StudentName;
                sheet.Cell(row, 2).Value = summary.StudentNo;

                var column = 3;
                foreach (var preset in ScoreCategories.Presets)
                {
                    sheet.Cell(row, column).Value = summary.CategoryScores.TryGetValue(preset, out var score) ? score : 0m;
                    column++;
                }

                sheet.Cell(row, column).Value = summary.TotalScore;
                row++;
            }

            sheet.Row(1).Style.Font.Bold = true;
            sheet.Columns().AdjustToContents();
            sheet.SheetView.FreezeRows(1);

            // Sheet2：明细记录（该学期全部记录，按日期升序）
            var detail = workbook.Worksheets.Add("明细记录");
            detail.Cell(1, 1).Value = "日期";
            detail.Cell(1, 2).Value = "学号";
            detail.Cell(1, 3).Value = "姓名";
            detail.Cell(1, 4).Value = "类别";
            detail.Cell(1, 5).Value = "分数";
            detail.Cell(1, 6).Value = "说明";
            detail.Cell(1, 7).Value = "佐证";

            var detailRow = 2;
            foreach (var entry in entries)
            {
                detail.Cell(detailRow, 1).Value = entry.EntryDateText;
                detail.Cell(detailRow, 2).Value = entry.StudentNo;
                detail.Cell(detailRow, 3).Value = entry.StudentName;
                detail.Cell(detailRow, 4).Value = entry.Category;
                detail.Cell(detailRow, 5).Value = entry.Score;
                detail.Cell(detailRow, 5).Style.NumberFormat.Format = "0.##";
                detail.Cell(detailRow, 6).Value = entry.Description;
                detail.Cell(detailRow, 7).Value = entry.Evidence ?? string.Empty;
                detailRow++;
            }

            detail.Row(1).Style.Font.Bold = true;
            detail.Columns().AdjustToContents();
            detail.SheetView.FreezeRows(1);

            workbook.SaveAs(filePath);
            return Task.FromResult(ExportResult.Ok(filePath));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ExportResult.Fail($"导出失败：{ex.Message}"));
        }
    }

    /// <summary>导出目录与数据库同级的 data/exports 下；学期名里的路径分隔符做替换。</summary>
    private static string BuildFilePath(string semester)
    {
        var databaseFilePath = DatabasePathProvider.GetDatabaseFilePath();
        var dataDirectory = Path.GetDirectoryName(databaseFilePath) ?? AppContext.BaseDirectory;
        var safeSemester = semester.Replace('/', '-').Replace('\\', '-');
        return Path.Combine(dataDirectory, "exports", $"综测汇总_{safeSemester}.xlsx");
    }
}
