using System.IO;
using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using ClosedXML.Excel;

namespace ClassScheduleApp.Services;

/// <summary>
/// 基于 ClosedXML 的班费账单导出实现。导出失败一律转成 ExportResult.Fail，不影响界面。
/// </summary>
public sealed class ExpenseExportService : IExpenseExportService
{
    /// <inheritdoc />
    public Task<ExportResult> ExportAsync(
        DateOnly start,
        DateOnly end,
        ExpenseSummary summary,
        IReadOnlyList<ExpenseRecordInfo> entries,
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

            // Sheet1：汇总
            var sheet = workbook.Worksheets.Add("汇总");
            sheet.Cell(1, 1).Value = "统计区间";
            sheet.Cell(1, 2).Value = $"{start:yyyy-MM-dd} ~ {end:yyyy-MM-dd}";

            sheet.Cell(3, 1).Value = "项目";
            sheet.Cell(3, 2).Value = "金额";
            sheet.Cell(4, 1).Value = "总收入";
            sheet.Cell(4, 2).Value = summary.TotalIncome;
            sheet.Cell(5, 1).Value = "总支出";
            sheet.Cell(5, 2).Value = summary.TotalExpense;
            sheet.Cell(6, 1).Value = "当前余额";
            sheet.Cell(6, 2).Value = summary.Balance;
            sheet.Cell(7, 1).Value = "笔数";
            sheet.Cell(7, 2).Value = summary.EntryCount;

            for (var row = 4; row <= 6; row++)
            {
                sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            }

            sheet.Range(3, 1, 3, 2).Style.Font.Bold = true;

            // 按类别小计表
            var startRow = 9;
            sheet.Cell(startRow, 1).Value = "类别";
            sheet.Cell(startRow, 2).Value = "收入";
            sheet.Cell(startRow, 3).Value = "支出";
            sheet.Cell(startRow, 4).Value = "净额";
            sheet.Cell(startRow, 5).Value = "笔数";
            sheet.Range(startRow, 1, startRow, 5).Style.Font.Bold = true;

            var subtotalRow = startRow + 1;
            foreach (var subtotal in summary.SubTotals)
            {
                sheet.Cell(subtotalRow, 1).Value = subtotal.Category;
                sheet.Cell(subtotalRow, 2).Value = subtotal.IncomeAmount;
                sheet.Cell(subtotalRow, 3).Value = subtotal.ExpenseAmount;
                sheet.Cell(subtotalRow, 4).Value = subtotal.NetAmount;
                sheet.Cell(subtotalRow, 5).Value = subtotal.EntryCount;
                sheet.Range(subtotalRow, 2, subtotalRow, 4).Style.NumberFormat.Format = "#,##0.00";
                subtotalRow++;
            }

            sheet.Columns().AdjustToContents();

            // Sheet2：明细流水（日期升序）
            var detail = workbook.Worksheets.Add("明细流水");
            detail.Cell(1, 1).Value = "日期";
            detail.Cell(1, 2).Value = "类型";
            detail.Cell(1, 3).Value = "类别";
            detail.Cell(1, 4).Value = "金额";
            detail.Cell(1, 5).Value = "说明";
            detail.Cell(1, 6).Value = "经办人";
            detail.Cell(1, 7).Value = "备注";

            var detailRow = 2;
            foreach (var entry in entries)
            {
                detail.Cell(detailRow, 1).Value = entry.Date.ToString("yyyy-MM-dd");
                detail.Cell(detailRow, 2).Value = entry.TypeText;
                detail.Cell(detailRow, 3).Value = entry.Category;
                detail.Cell(detailRow, 4).Value = entry.Type == ExpenseType.Income ? entry.Amount : -entry.Amount;
                detail.Cell(detailRow, 4).Style.NumberFormat.Format = "#,##0.00";
                detail.Cell(detailRow, 5).Value = entry.Description;
                detail.Cell(detailRow, 6).Value = entry.Handler;
                detail.Cell(detailRow, 7).Value = entry.Note ?? string.Empty;
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
        var fileName = $"班费账单_{start:yyyy-MM-dd}_{end:yyyy-MM-dd}.xlsx";
        return Path.Combine(dataDirectory, "exports", fileName);
    }
}
