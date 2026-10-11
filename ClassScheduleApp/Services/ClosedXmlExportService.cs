using System.IO;
using ClassScheduleApp.Data;
using ClassScheduleApp.Repositories;
using ClosedXML.Excel;

namespace ClassScheduleApp.Services;

/// <summary>
/// 基于 ClosedXML 的导出实现：生成 data/exports/无课表_{semester}_第{week}周.xlsx。
/// 导出失败一律转成 ExportResult.Fail，不让界面崩溃。
/// </summary>
public sealed class ClosedXmlExportService : IExportService
{
    private const int DayCount = 7;
    private const int SlotCount = 6;

    /// <inheritdoc />
    public Task<ExportResult> ExportFreeTimeGridAsync(
        string semester,
        int week,
        IReadOnlyList<ClassFreeCell> cells,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = BuildFilePath(semester, week);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // (星期, 节次对下标) → 格子
            var lookup = new Dictionary<(int Day, int Slot), ClassFreeCell>();
            var periodTextBySlot = new Dictionary<int, string>();
            foreach (var cell in cells)
            {
                lookup[(cell.DayOfWeek, cell.SlotIndex)] = cell;
                periodTextBySlot[cell.SlotIndex] = cell.PeriodText;
            }

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add($"第{week}周");

            // 表头：第一列是节次，之后 7 列是周一~周日
            sheet.Cell(1, 1).Value = $"第{week}周 节次";
            for (var day = 1; day <= DayCount; day++)
            {
                sheet.Cell(1, day + 1).Value = WeekdayText(day);
            }

            for (var slot = 0; slot < SlotCount; slot++)
            {
                var row = slot + 2;
                sheet.Cell(row, 1).Value = periodTextBySlot.TryGetValue(slot, out var periodText)
                    ? periodText
                    : string.Empty;

                for (var day = 1; day <= DayCount; day++)
                {
                    var target = sheet.Cell(row, day + 1);
                    if (lookup.TryGetValue((day, slot), out var cell))
                    {
                        // 全班空闲的格填"全班空闲"，否则逐行列出空闲学生姓名
                        target.Value = cell.IsAllFree
                            ? "全班空闲"
                            : string.Join("\n", cell.FreeStudentNames);
                        target.Style.Alignment.WrapText = true;
                    }
                    else
                    {
                        target.Value = string.Empty;
                    }
                }
            }

            sheet.Column(1).Width = 14;
            for (var day = 1; day <= DayCount; day++)
            {
                sheet.Column(day + 1).Width = 20;
            }

            sheet.SheetView.FreezeRows(1);
            workbook.SaveAs(filePath);

            return Task.FromResult(ExportResult.Ok(filePath));
        }
        catch (Exception ex)
        {
            // 导出属于边缘操作：任何异常都转成友好提示，不影响主流程
            return Task.FromResult(ExportResult.Fail($"导出失败：{ex.Message}"));
        }
    }

    /// <summary>导出目录与数据库同级的 data/exports 下。</summary>
    private static string BuildFilePath(string semester, int week)
    {
        var databaseFilePath = DatabasePathProvider.GetDatabaseFilePath();
        var dataDirectory = Path.GetDirectoryName(databaseFilePath) ?? AppContext.BaseDirectory;
        var fileName = $"无课表_{Sanitize(semester)}_第{week}周.xlsx";
        return Path.Combine(dataDirectory, "exports", fileName);
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            foreach (var c in invalid)
            {
                if (chars[i] == c)
                {
                    chars[i] = '_';
                    break;
                }
            }
        }

        return new string(chars);
    }

    private static string WeekdayText(int dayOfWeek)
    {
        return dayOfWeek switch
        {
            1 => "周一",
            2 => "周二",
            3 => "周三",
            4 => "周四",
            5 => "周五",
            6 => "周六",
            7 => "周日",
            _ => $"星期{dayOfWeek}",
        };
    }
}
