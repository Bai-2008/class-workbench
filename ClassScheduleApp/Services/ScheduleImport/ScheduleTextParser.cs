using System.Globalization;
using System.Text.RegularExpressions;
using ClassScheduleApp.Models;

namespace ClassScheduleApp.Services.ScheduleImport;

/// <summary>
/// 教务导出课表的文本解析器（纯函数、无状态、无网络）。
///
/// ============ 格式规格（由两份真实样本推导，供人工核对）============
/// 样本：示例学生甲(2026-2027-1)课表.pdf、示例学生乙(2026-2027-1)课表.pdf（PDF 取文后为纯文本）。
///
/// 1) 头部（通常第 1 行，姓名/学号同在一行）：
///      "2026-2027学年第1学期 示例学生甲课表 学号：202611610000"
///      · 姓名 = "课表"二字之前、与前面内容以空白分隔的那段文本
///      · 学号 = "学号："之后连续的数字
///      · 头部缺失不视为致命错误（只要能解析出课程行）
///
/// 2) 星期块标题：单独一行，形如"星期一"…"星期日"（兼容"周一"…"周日"）。
///    其后的课程行都归属该星期，直到出现下一个星期块标题。
///
/// 3) 课程行（每个时段一行）：
///      "3-4 法理学★ 周数: 4-19周/地点: 7C308/教师: 任淑娇/教学班:法理学-0001/教学班组成:…/学分:3.0"
///      · 起止节次 = 行首的"数字-数字"；**连堂以该区间表达**（教务把连堂合并为一格）
///      · 课程名 = 节次之后、到第一个"周数"之前；行尾的 ★ ○ ● ◇ 是课程性质符号，需剥离
///      · 周次 = "周数:"后到下一个"/"之前，形如 4-19周 / 5-19周(单) / 13-16周
///          - 含"单" → 单周；含"双" → 双周；否则每周（以学期教学周序号为准，第 1、3、5… 周为单周）
///          - **任意周次列表**（如"1,3,5周"）→ 该行标"暂不支持"并跳过，不影响其余行
///      · 地点 / 教师 / 学分 均按 "键: 值" 读取（值到下一个"/"结束）；学分可空
///      · 编号：教务导出不含课程编号 → 一律为空（应用内编号本就选填）
///
/// 4) 尾部附注：以"其他课程"开头的行、以及图例行（"★: 讲课 ○: 实验 …"、"打印时间:…"）一律忽略。
///
/// 5) 容错：
///      · 整体无法识别（没有任何课程行，且没有任何可归因的行）→ IsRecognized=false + FailureReason
///      · 个别行失败 → 记入 Skipped（含原文与原因），其余行照常解析
///      · 课程行出现在任何星期块之前 → 跳过并说明"未遇到星期块"
/// ================================================================
/// </summary>
public sealed class ScheduleTextParser : IScheduleTextParser
{
    /// <summary>课程行：行首"起节-止节"。</summary>
    private static readonly Regex PeriodRegex = new(@"^\s*(\d{1,2})\s*[-—–~～]\s*(\d{1,2})\s+(?<rest>.+)$", RegexOptions.Compiled);

    /// <summary>星期块标题。</summary>
    private static readonly Regex WeekdayRegex = new(@"^\s*(?:星期|周)\s*([一二三四五六日天1-7])\s*$", RegexOptions.Compiled);

    /// <summary>头部：<姓名>课表。</summary>
    private static readonly Regex HeaderNameRegex = new(@"(?<name>[^\s:：]{1,20})\s*课表", RegexOptions.Compiled);

    /// <summary>头部：学号。</summary>
    private static readonly Regex HeaderNoRegex = new(@"学号\s*[:：]\s*(?<no>\d{4,20})", RegexOptions.Compiled);

    private static readonly Regex WeekRangeRegex = new(@"(\d{1,2})\s*[-—–~～至]\s*(\d{1,2})", RegexOptions.Compiled);
    private static readonly Regex SingleWeekRegex = new(@"(\d{1,2})\s*周", RegexOptions.Compiled);
    private static readonly Regex LocationRegex = new(@"地点\s*[:：]\s*(?<value>[^/]*)", RegexOptions.Compiled);
    private static readonly Regex TeacherRegex = new(@"教师\s*[:：]\s*(?<value>[^/]*)", RegexOptions.Compiled);
    private static readonly Regex CreditRegex = new(@"学分\s*[:：]\s*(?<value>[0-9]+(?:\.[0-9]+)?)", RegexOptions.Compiled);
    private static readonly Regex WeekFieldRegex = new(@"周数\s*[:：]\s*(?<value>[^/]*)", RegexOptions.Compiled);

    /// <summary>课程性质符号（讲课/实验/实践/上机），出现于课程名尾部。</summary>
    private static readonly char[] NatureMarks = { '★', '○', '●', '◇' };

    /// <summary>图例行：以性质符号开头并紧跟冒号。</summary>
    private static readonly Regex LegendRegex = new(@"^\s*[★○●◇]\s*[:：]", RegexOptions.Compiled);

    /// <inheritdoc />
    public ParsedSchedule Parse(string content)
    {
        var result = new ParsedSchedule();

        if (string.IsNullOrWhiteSpace(content))
        {
            result.IsRecognized = false;
            result.FailureReason = "文件内容为空。";
            return result;
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var headerFound = false;
        var dayOfWeek = 0;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            // 头部（可能出现在前几行）
            if (!headerFound && (line.Contains("课表") || line.Contains("学号")))
            {
                var nameMatch = HeaderNameRegex.Match(line);
                var noMatch = HeaderNoRegex.Match(line);
                if (nameMatch.Success)
                {
                    result.Header.StudentName = nameMatch.Groups["name"].Value.Trim();
                    headerFound = true;
                }

                if (noMatch.Success)
                {
                    result.Header.StudentNo = noMatch.Groups["no"].Value;
                    headerFound = true;
                }

                if (headerFound && !PeriodRegex.IsMatch(line))
                {
                    continue;
                }
            }

            // 尾部附注与图例（图例行形如 "★: 讲课 ○: 实验 ●: 实践 ◇: 上机"，注意不能用 Contains("实践")
            // 之类判断，否则会误伤名字里带"实践"的正常课程行）
            if (line.StartsWith("其他课程", StringComparison.Ordinal) ||
                line.StartsWith("打印时间", StringComparison.Ordinal) ||
                LegendRegex.IsMatch(line))
            {
                continue;
            }

            // 星期块标题
            var weekdayMatch = WeekdayRegex.Match(line);
            if (weekdayMatch.Success)
            {
                dayOfWeek = ParseWeekday(weekdayMatch.Groups[1].Value);
                continue;
            }

            // 课程行
            var periodMatch = PeriodRegex.Match(line);
            if (!periodMatch.Success)
            {
                // 无法归因的行：记入跳过（不阻塞其余行）
                result.Skipped.Add(new ParsedScheduleSkip(line, "不是可识别的课程行（缺少行首节次）"));
                continue;
            }

            if (dayOfWeek == 0)
            {
                result.Skipped.Add(new ParsedScheduleSkip(line, "该行之前未遇到星期块，无法确定星期"));
                continue;
            }

            var startPeriod = int.Parse(periodMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var endPeriod = int.Parse(periodMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            var rest = periodMatch.Groups["rest"].Value;

            if (startPeriod < 1 || endPeriod < startPeriod || endPeriod > 24)
            {
                result.Skipped.Add(new ParsedScheduleSkip(line, $"节次不合法（{startPeriod}-{endPeriod}）"));
                continue;
            }

            var weekField = WeekFieldRegex.Match(rest);
            if (!weekField.Success)
            {
                result.Skipped.Add(new ParsedScheduleSkip(line, "缺少「周数」字段"));
                continue;
            }

            var weekValue = weekField.Groups["value"].Value.Trim();
            if (weekValue.Contains(',') || weekValue.Contains('，') || weekValue.Contains('、'))
            {
                result.Skipped.Add(new ParsedScheduleSkip(line, $"暂不支持任意周次列表（{weekValue}）"));
                continue;
            }

            int startWeek;
            int endWeek;
            var rangeMatch = WeekRangeRegex.Match(weekValue);
            if (rangeMatch.Success)
            {
                startWeek = int.Parse(rangeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                endWeek = int.Parse(rangeMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            }
            else
            {
                var single = SingleWeekRegex.Match(weekValue);
                if (!single.Success)
                {
                    result.Skipped.Add(new ParsedScheduleSkip(line, $"周次无法识别（{weekValue}）"));
                    continue;
                }

                startWeek = int.Parse(single.Groups[1].Value, CultureInfo.InvariantCulture);
                endWeek = startWeek;
            }

            if (startWeek < 1 || endWeek < startWeek || endWeek > 40)
            {
                result.Skipped.Add(new ParsedScheduleSkip(line, $"周次范围不合法（{startWeek}-{endWeek}）"));
                continue;
            }

            // 课程名 = 节次之后到"周数"之前，剥离课程性质符号
            var nameEnd = weekField.Index;
            var courseName = rest[..nameEnd].Trim().TrimEnd(NatureMarks).Trim();
            if (courseName.Length == 0)
            {
                result.Skipped.Add(new ParsedScheduleSkip(line, "课程名为空"));
                continue;
            }

            var creditText = CreditRegex.Match(rest).Groups["value"].Value;
            double? credit = double.TryParse(creditText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedCredit)
                ? parsedCredit
                : null;

            result.Entries.Add(new ParsedScheduleEntry
            {
                CourseName = courseName,
                Teacher = TeacherRegex.Match(rest).Groups["value"].Value.Trim(),
                Location = LocationRegex.Match(rest).Groups["value"].Value.Trim(),
                DayOfWeek = dayOfWeek,
                StartPeriod = startPeriod,
                EndPeriod = endPeriod,
                StartWeek = startWeek,
                EndWeek = endWeek,
                WeekParity = weekValue.Contains('单') ? WeekParityTypes.Odd
                    : weekValue.Contains('双') ? WeekParityTypes.Even
                    : WeekParityTypes.Every,
                CourseNo = null,
                Credit = credit,
            });
        }

        if (result.Entries.Count == 0)
        {
            result.IsRecognized = false;
            result.FailureReason = result.Skipped.Count == 0
                ? "无法识别的格式：没有任何课程行。"
                : $"无法识别的格式：共 {result.Skipped.Count} 行均无法解析（首行原因：{result.Skipped[0].Reason}）。";
        }

        return result;
    }

    private static int ParseWeekday(string text)
    {
        return text switch
        {
            "一" or "1" => 1,
            "二" or "2" => 2,
            "三" or "3" => 3,
            "四" or "4" => 4,
            "五" or "5" => 5,
            "六" or "6" => 6,
            "日" or "天" or "7" => 7,
            _ => 0,
        };
    }
}
