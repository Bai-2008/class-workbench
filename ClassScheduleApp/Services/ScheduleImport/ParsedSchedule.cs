namespace ClassScheduleApp.Services.ScheduleImport;

/// <summary>
/// 解析出的学生头部信息（姓名 / 学号），教务导出里可能缺失。
/// </summary>
public sealed class ParsedScheduleHeader
{
    /// <summary>学生姓名；未识别到时为 null。</summary>
    public string? StudentName { get; set; }

    /// <summary>学号；未识别到时为 null。</summary>
    public string? StudentNo { get; set; }
}

/// <summary>
/// 解析出的一条课程时段（未落库）。单双周取值见 <see cref="Models.WeekParityTypes"/>。
/// </summary>
public sealed class ParsedScheduleEntry
{
    /// <summary>课程名（已剥离课程性质符号）。</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>任课教师。</summary>
    public string Teacher { get; set; } = string.Empty;

    /// <summary>上课地点。</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>星期，1–7。</summary>
    public int DayOfWeek { get; set; }

    /// <summary>起始节次。</summary>
    public int StartPeriod { get; set; }

    /// <summary>结束节次（连堂时大于起始节次）。</summary>
    public int EndPeriod { get; set; }

    /// <summary>起始周。</summary>
    public int StartWeek { get; set; }

    /// <summary>结束周。</summary>
    public int EndWeek { get; set; }

    /// <summary>单双周：0 每周 / 1 单周 / 2 双周。</summary>
    public int WeekParity { get; set; }

    /// <summary>课程编号（教务导出通常没有，可空）。</summary>
    public string? CourseNo { get; set; }

    /// <summary>学分（可空）。</summary>
    public double? Credit { get; set; }

    /// <summary>是否被人工剔除（预览界面可逐行剔除）。</summary>
    public bool IsExcluded { get; set; }

    /// <summary>单双周展示文本（每周为空）。</summary>
    public string ParityText => WeekParity == Models.WeekParityTypes.Every
        ? string.Empty
        : Models.WeekParityTypes.ToDisplay(WeekParity);

    /// <summary>星期展示文本。</summary>
    public string WeekdayText => DayOfWeek switch
    {
        1 => "周一",
        2 => "周二",
        3 => "周三",
        4 => "周四",
        5 => "周五",
        6 => "周六",
        7 => "周日",
        _ => $"星期{DayOfWeek}",
    };

    /// <summary>节次展示文本。</summary>
    public string PeriodText => StartPeriod == EndPeriod ? $"{StartPeriod}节" : $"{StartPeriod}-{EndPeriod}节";

    /// <summary>周次展示文本。</summary>
    public string WeekText => $"{StartWeek}-{EndWeek}周";

    /// <summary>摘要文本（预览列表用）。</summary>
    public string Summary => $"{WeekdayText} {PeriodText} {WeekText}{(ParityText.Length > 0 ? " " + ParityText : string.Empty)} {CourseName} {Teacher} {Location}".Trim();
}

/// <summary>
/// 一行解析失败的记录：原文 + 原因（不阻塞其余行）。
/// </summary>
public sealed class ParsedScheduleSkip
{
    public ParsedScheduleSkip(string rawLine, string reason)
    {
        RawLine = rawLine;
        Reason = reason;
    }

    /// <summary>原始行文本。</summary>
    public string RawLine { get; }

    /// <summary>跳过原因。</summary>
    public string Reason { get; }
}

/// <summary>
/// 解析结果：头部 + 成功行 + 跳过行；<see cref="IsRecognized"/> 为 false 表示整体无法识别。
/// </summary>
public sealed class ParsedSchedule
{
    /// <summary>学生头部信息（可能为空）。</summary>
    public ParsedScheduleHeader Header { get; set; } = new();

    /// <summary>成功解析的课程时段。</summary>
    public List<ParsedScheduleEntry> Entries { get; set; } = new();

    /// <summary>跳过（失败）的行及原因。</summary>
    public List<ParsedScheduleSkip> Skipped { get; set; } = new();

    /// <summary>是否被识别为教务课表格式。</summary>
    public bool IsRecognized { get; set; } = true;

    /// <summary>整体无法识别时的说明。</summary>
    public string? FailureReason { get; set; }
}
