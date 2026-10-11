using System.IO;
using ClassScheduleApp.Data;
using ClassScheduleApp.Repositories;
using ClosedXML.Excel;

namespace ClassScheduleApp.Services;

/// <summary>
/// 基于 ClosedXML 的考勤导出实现。
/// 导出失败一律转成 ExportResult.Fail，不影响界面。
/// </summary>
public sealed class AttendanceExportService : IAttendanceExportService
{
    /// <inheritdoc />
    public Task<ExportResult> ExportAttendanceAsync(
        DateOnly start,
        DateOnly end,
        IReadOnlyList<StudentAttendanceStats> studentStats,
        IReadOnlyList<AttendanceRecordInfo> records,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = BuildFilePath(start, end);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var workbook = new XLWorkbook();

            // Sheet1：按学生汇总（列与界面一致）
            var summary = workbook.Worksheets.Add("按学生汇总");
            summary.Cell(1, 1).Value = "姓名";
            summary.Cell(1, 2).Value = "学号";
            summary.Cell(1, 3).Value = "出勤";
            summary.Cell(1, 4).Value = "迟到";
            summary.Cell(1, 5).Value = "旷课";
            summary.Cell(1, 6).Value = "请假";
            summary.Cell(1, 7).Value = "出勤率";

            var row = 2;
            foreach (var stats in studentStats)
            {
                summary.Cell(row, 1).Value = stats.StudentName;
                summary.Cell(row, 2).Value = stats.StudentNo;
                summary.Cell(row, 3).Value = stats.PresentCount;
                summary.Cell(row, 4).Value = stats.LateCount;
                summary.Cell(row, 5).Value = stats.AbsentCount;
                summary.Cell(row, 6).Value = stats.LeaveCount;
                summary.Cell(row, 7).Value = stats.HasRecords ? stats.AttendanceRate : 0d;
                summary.Cell(row, 7).Style.NumberFormat.Format = "0.0%";
                row++;
            }

            summary.Row(1).Style.Font.Bold = true;
            summary.Columns().AdjustToContents();

            // Sheet2：明细记录
            var detail = workbook.Worksheets.Add("明细记录");
            detail.Cell(1, 1).Value = "日期";
            detail.Cell(1, 2).Value = "学生";
            detail.Cell(1, 3).Value = "课程";
            detail.Cell(1, 4).Value = "状态";
            detail.Cell(1, 5).Value = "备注";

            var detailRow = 2;
            foreach (var record in records)
            {
                detail.Cell(detailRow, 1).Value = record.Date.ToString("yyyy-MM-dd");
                detail.Cell(detailRow, 2).Value = $"{record.StudentName}（{record.StudentNo}）";
                detail.Cell(detailRow, 3).Value = record.OccasionText;
                detail.Cell(detailRow, 4).Value = record.StatusText;
                detail.Cell(detailRow, 5).Value = record.Note ?? string.Empty;
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

    /// <summary>导出目录与数据库同级的 data/exports 下。</summary>
    private static string BuildFilePath(DateOnly start, DateOnly end)
    {
        var databaseFilePath = DatabasePathProvider.GetDatabaseFilePath();
        var dataDirectory = Path.GetDirectoryName(databaseFilePath) ?? AppContext.BaseDirectory;
        var fileName = $"考勤统计_{start:yyyy-MM-dd}_{end:yyyy-MM-dd}.xlsx";
        return Path.Combine(dataDirectory, "exports", fileName);
    }
}
