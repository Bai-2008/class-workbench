namespace ClassScheduleApp.Repositories;

/// <summary>
/// 课程维护用的数据对象：课程字段 + 排课时间。
/// 课程列表与课程编辑共用该类型，避免把实体直接暴露给界面层。
/// <see cref="Schedules"/> 是该课程的全部时段（支持一周多次）；
/// <see cref="ScheduleId"/>/<see cref="DayOfWeek"/> 等单时段成员保留，供课程列表与其它模块继续使用（取第一个时段）。
/// </summary>
public class CourseDetail
{
    /// <summary>课程主键；0 表示新增。</summary>
    public int Id { get; set; }

    /// <summary>课程编号（选填、唯一）。留空表示未填写。</summary>
    public string? CourseNo { get; set; }

    /// <summary>课程名称。</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>任课教师。</summary>
    public string Teacher { get; set; } = string.Empty;

    /// <summary>学分（选填）。留空表示未填写。</summary>
    public double? Credit { get; set; }

    /// <summary>该课程的全部上课时段（按星期、起始节次升序）。</summary>
    public IReadOnlyList<CourseScheduleDetail> Schedules { get; set; } = new List<CourseScheduleDetail>();

    /// <summary>排课主键；0 表示该课程还没有排课记录（取第一个时段）。</summary>
    public int ScheduleId { get; set; }

    /// <summary>星期，1–7（1=周一）（取第一个时段）。</summary>
    public int DayOfWeek { get; set; }

    /// <summary>起始节次（取第一个时段）。</summary>
    public int StartPeriod { get; set; }

    /// <summary>结束节次（取第一个时段）。</summary>
    public int EndPeriod { get; set; }

    /// <summary>起始周（取第一个时段）。</summary>
    public int StartWeek { get; set; } = 1;

    /// <summary>结束周（取第一个时段）。</summary>
    public int EndWeek { get; set; } = 20;

    /// <summary>上课教室（取第一个时段）。</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>单双周（取第一个时段）：0 每周 / 1 单周 / 2 双周。</summary>
    public int WeekParity { get; set; }

    /// <summary>单双周展示文本；每周时为空串（列表据此决定是否加小标）。</summary>
    public string ParityText => WeekParity == Models.WeekParityTypes.Every
        ? string.Empty
        : Models.WeekParityTypes.ToDisplay(WeekParity);

    /// <summary>关联学生数（课程管理列表用）。</summary>
    public int StudentCount { get; set; }

    /// <summary>零关联标注：无学生时显示「无学生」，否则为空。</summary>
    public string NoStudentLabel => StudentCount == 0 ? "无学生" : StudentCount.ToString();

    /// <summary>编号展示文本；未填写时显示占位。</summary>
    public string CourseNoText => string.IsNullOrWhiteSpace(CourseNo) ? "（未填）" : CourseNo;

    /// <summary>学分展示文本；未填写时显示占位。</summary>
    public string CreditText => Credit.HasValue ? Credit.Value.ToString("0.##") : "（未填）";

    /// <summary>星期的展示文本（取第一个时段）。</summary>
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

    /// <summary>节次的展示文本，如 1-2节（取第一个时段）。</summary>
    public string PeriodText => $"{StartPeriod}-{EndPeriod}节";
}
