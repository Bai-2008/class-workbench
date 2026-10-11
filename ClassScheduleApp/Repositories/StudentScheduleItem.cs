namespace ClassScheduleApp.Repositories;

/// <summary>
/// 学生课表条目（展示模型），由 StudentRepository 组装完成后返回给 ViewModel。
/// 只包含界面需要的字段，不含实体间的导航关系。
/// </summary>
public class StudentScheduleItem
{
    /// <summary>课程 Id（用于打开既有课程编辑弹窗；禁止用课程名等文字字段做关联）。</summary>
    public int CourseId { get; set; }

    /// <summary>课程名称。</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>授课老师。</summary>
    public string Teacher { get; set; } = string.Empty;

    /// <summary>星期，1–7（1=周一）。</summary>
    public int DayOfWeek { get; set; }

    /// <summary>起始节次。</summary>
    public int StartPeriod { get; set; }

    /// <summary>结束节次。</summary>
    public int EndPeriod { get; set; }

    /// <summary>上课教室。</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>起始周。</summary>
    public int StartWeek { get; set; } = 1;

    /// <summary>结束周。</summary>
    public int EndWeek { get; set; } = 20;

    /// <summary>单双周：0 每周 / 1 单周 / 2 双周（见 <see cref="Models.WeekParityTypes"/>）。</summary>
    public int WeekParity { get; set; }

    /// <summary>
    /// 列表展示文本：课程名 + 「星期 节次」。同名师在不同开课班上课时，
    /// 列表靠这段文本区分（仅展示层，不参与任何身份判定）。
    /// </summary>
    public string ScheduleDisplayText
    {
        get
        {
            var day = DayOfWeek switch
            {
                1 => "周一",
                2 => "周二",
                3 => "周三",
                4 => "周四",
                5 => "周五",
                6 => "周六",
                _ => "周日",
            };
            var periods = StartPeriod == EndPeriod ? $"{StartPeriod}节" : $"{StartPeriod}-{EndPeriod}节";
            return $"{CourseName}（{day} {periods}）";
        }
    }

    /// <summary>单双周展示文本；每周时为空串（界面据此决定是否加小标）。</summary>
    public string ParityText => WeekParity == Models.WeekParityTypes.Every
        ? string.Empty
        : Models.WeekParityTypes.ToDisplay(WeekParity);

    /// <summary>周次展示文本，如 1-16周。</summary>
    public string WeekRangeText => $"{StartWeek}-{EndWeek}周";

    /// <summary>
    /// 本节次的起止时刻文本（如 "08:30-10:05"），由页面 ViewModel 依据作息时间表填充；
    /// 作息未载入时为空串（界面降级只显示节次）。
    /// </summary>
    public string PeriodTimeText { get; set; } = string.Empty;

    /// <summary>星期的展示文本。</summary>
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

    /// <summary>节次的展示文本，如 1-2节。</summary>
    public string PeriodText => $"{StartPeriod}-{EndPeriod}节";

    /// <summary>供下拉读取显示文本（WPF 的项名称取 ToString，避免显示类型全名）。</summary>
    public override string ToString() => CourseName;
}
