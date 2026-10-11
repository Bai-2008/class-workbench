using System.IO;
using System.Text;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using ClosedXML.Excel;

namespace ClassScheduleApp.Services;

/// <summary>
/// 批量导入学生实现：列映射按表头文本识别（学号/姓名必填，其余映射 Student 现有字段），
/// 身份键 = 学号（StudentNo 唯一），已存在即跳过（纯增量，不覆盖不修改）。
/// xlsx 复用既有 ClosedXML 依赖（MIT，无需安装 Office）。
/// </summary>
public sealed class StudentImportService : IStudentImportService
{
    private readonly IStudentAdminRepository _repository;

    public StudentImportService(IStudentAdminRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public string FileFilter => "学生名单 (*.xlsx;*.csv)|*.xlsx;*.csv|Excel 文件 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";

    /// <inheritdoc />
    public async Task<StudentImportReport> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var report = new StudentImportReport();

        if (!File.Exists(filePath))
        {
            report.FailedCount = 1;
            report.Failures.Add($"文件不存在：{filePath}");
            return report;
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        List<IReadOnlyList<string>> rows;
        try
        {
            rows = extension switch
            {
                ".xlsx" => ReadXlsx(filePath, report),
                ".csv" => ReadCsv(filePath, report),
                _ => throw new NotSupportedException($"不支持的文件类型：{extension}（支持 .xlsx / .csv）"),
            };
        }
        catch (Exception ex)
        {
            report.FailedCount = 1;
            report.Failures.Add($"读取失败：{ex.Message}");
            report.Source = Path.GetFileName(filePath);
            return report;
        }

        report.Source = $"{Path.GetFileName(filePath)}（{rows.Count} 行数据）";

        if (rows.Count == 0)
        {
            report.Failures.Add("文件中没有数据行（空文件或只有表头）。库未做任何改动。");
            return report;
        }

        // 表头映射：学号 / 姓名 必填，其余按 Student 现有字段映射
        var header = rows[0];
        var map = BuildColumnMap(header);
        if (map.StudentNo < 0 || map.Name < 0)
        {
            report.FailedCount = 1;
            report.Failures.Add("缺少必填列：需要同时存在「学号」与「姓名」两列表头。库未做任何改动。");
            return report;
        }

        // 纯表头（只有 1 行）→ 明确提示，而不是返回一份空报告
        if (rows.Count == 1)
        {
            report.Failures.Add("文件里只有表头，没有数据行。库未做任何改动。");
            return report;
        }

        report.Source += $"，列映射：" + DescribeMap(map);

        var existing = await _repository.GetAllStudentNosAsync(cancellationToken);
        var seenInFile = new HashSet<string>(StringComparer.Ordinal);
        var toAdd = new List<Student>();

        for (var index = 1; index < rows.Count; index++)
        {
            var row = rows[index];
            var lineNo = index + 1;
            var studentNo = Field(row, map.StudentNo);
            var name = Field(row, map.Name);

            if (studentNo.Length == 0)
            {
                report.SkippedCount++;
                report.Skipped.Add($"第 {lineNo} 行：缺学号（姓名「{name}」），已跳过");
                continue;
            }

            if (name.Length == 0)
            {
                report.SkippedCount++;
                report.Skipped.Add($"第 {lineNo} 行：缺姓名（学号 {studentNo}），已跳过");
                continue;
            }

            if (existing.Contains(studentNo))
            {
                report.SkippedCount++;
                report.Skipped.Add($"第 {lineNo} 行：学号 {studentNo}（{name}）已存在，跳过（不覆盖既有信息）");
                continue;
            }

            if (!seenInFile.Add(studentNo))
            {
                report.SkippedCount++;
                report.Skipped.Add($"第 {lineNo} 行：学号 {studentNo} 在本文件中重复，跳过");
                continue;
            }

            toAdd.Add(new Student
            {
                StudentNo = studentNo,
                Name = name,
                Gender = Field(row, map.Gender),
                ClassName = Field(row, map.ClassName),
                Phone = EmptyToNull(Field(row, map.Phone)),
                Note = EmptyToNull(Field(row, map.Note)),
            });
        }

        if (toAdd.Count == 0)
        {
            report.SkippedCount += 0;
            return report;
        }

        var result = await _repository.AddRangeAsync(toAdd, cancellationToken);
        if (!result.Success)
        {
            report.FailedCount = toAdd.Count;
            report.Failures.Add($"写入失败：{result.ErrorMessage ?? "未知错误"}（本次未新增任何人）");
            return report;
        }

        report.AddedCount = toAdd.Count;
        foreach (var student in toAdd)
        {
            report.Added.Add($"{student.StudentNo} {student.Name}");
        }

        return report;
    }

    /// <summary>列映射结果：-1 表示该列不存在。</summary>
    private readonly record struct ColumnMap(int StudentNo, int Name, int Gender, int ClassName, int Phone, int Note);

    private static ColumnMap BuildColumnMap(IReadOnlyList<string> header)
    {
        var no = -1;
        var name = -1;
        var gender = -1;
        var className = -1;
        var phone = -1;
        var note = -1;

        for (var i = 0; i < header.Count; i++)
        {
            var text = header[i].Trim();
            if (no < 0 && (text.Contains("学号", StringComparison.Ordinal) || text.Contains("学籍号", StringComparison.Ordinal)))
            {
                no = i;
            }
            else if (name < 0 && (text == "姓名" || text == "名字" || text == "学生姓名"))
            {
                name = i;
            }
            else if (gender < 0 && text.Contains("性别", StringComparison.Ordinal))
            {
                gender = i;
            }
            else if (className < 0 && (text.Contains("班级", StringComparison.Ordinal) || text.Contains("行政班", StringComparison.Ordinal)))
            {
                className = i;
            }
            else if (phone < 0 && (text.Contains("电话", StringComparison.Ordinal) || text.Contains("手机", StringComparison.Ordinal)))
            {
                phone = i;
            }
            else if (note < 0 && (text.Contains("备注", StringComparison.Ordinal) || text.Contains("说明", StringComparison.Ordinal)))
            {
                note = i;
            }
        }

        return new ColumnMap(no, name, gender, className, phone, note);
    }

    private static string DescribeMap(ColumnMap map)
    {
        var parts = new List<string> { $"学号=第{map.StudentNo + 1}列", $"姓名=第{map.Name + 1}列" };
        if (map.Gender >= 0) { parts.Add($"性别=第{map.Gender + 1}列"); }
        if (map.ClassName >= 0) { parts.Add($"班级=第{map.ClassName + 1}列"); }
        if (map.Phone >= 0) { parts.Add($"电话=第{map.Phone + 1}列"); }
        if (map.Note >= 0) { parts.Add($"备注=第{map.Note + 1}列"); }
        return string.Join("，", parts);
    }

    private static string Field(IReadOnlyList<string> row, int index)
        => index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    /// <summary>读取 xlsx：第一张工作表的首行为表头，取用到的右下角范围。</summary>
    private static List<IReadOnlyList<string>> ReadXlsx(string filePath, StudentImportReport report)
    {
        var rows = new List<IReadOnlyList<string>>();
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet is null)
        {
            report.Failures.Add("工作簿里没有任何工作表。");
            return rows;
        }

        var range = sheet.RangeUsed();
        if (range is null)
        {
            return rows;
        }

        foreach (var row in range.Rows())
        {
            var cells = new List<string>();
            foreach (var cell in row.Cells())
            {
                cells.Add(cell.GetFormattedString().Trim());
            }

            // 跳过整行为空的行
            if (cells.Any(c => c.Length > 0))
            {
                rows.Add(cells);
            }
        }

        return rows;
    }

    /// <summary>
    /// 读取 CSV：先按 UTF-8 严格解码，失败再按 GBK（936）解码；
    /// 两者都失败则报错。解决了"UTF-8/GBK 编码探测"。
    /// </summary>
    private static List<IReadOnlyList<string>> ReadCsv(string filePath, StudentImportReport report)
    {
        var bytes = File.ReadAllBytes(filePath);
        string text;
        string encodingName;

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            text = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            encodingName = "UTF-8（带 BOM）";
        }
        else
        {
            try
            {
                text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
                encodingName = "UTF-8";
            }
            catch (DecoderFallbackException)
            {
                text = GbkEncoding.GetString(bytes);
                encodingName = "GBK（自动探测）";
            }
        }

        report.Source = $"编码 {encodingName}；";

        var rows = new List<IReadOnlyList<string>>();
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (raw.Trim().Length == 0)
            {
                continue;
            }

            rows.Add(SplitCsvLine(raw));
        }

        return rows;
    }

    /// <summary>GBK 编码（.NET Core 需先注册 CodePages 提供程序）。</summary>
    private static Encoding GbkEncoding
    {
        get
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(936);
        }
    }

    /// <summary>按 CSV 规则切分一行（支持双引号包裹与 "" 转义）。</summary>
    private static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    builder.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',' || ch == '\t')
            {
                fields.Add(builder.ToString().Trim());
                builder.Clear();
            }
            else
            {
                builder.Append(ch);
            }
        }

        fields.Add(builder.ToString().Trim());
        return fields;
    }
}
