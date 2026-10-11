namespace ClassScheduleApp.Models;

/// <summary>
/// 排课实体，对应 CourseSchedules 表（一表一类）。仅通过 CourseId 外键关联课程。
/// </summary>
public class CourseSchedule
{
    /// <summary>主键。</summary>
    public int Id { get; set; }

    /// <summary>关联课程的外键。</summary>
    public int CourseId { get; set; }

    /// <summary>关联课程导航属性。</summary>
    public Course? Course { get; set; }

    /// <summary>星期，取值 1–7（1=周一 … 7=周日），数据库层有 CHECK 约束。</summary>
    public int DayOfWeek { get; set; }

    /// <summary>起始节次。</summary>
    public int StartPeriod { get; set; }

    /// <summary>结束节次，必须大于等于起始节次（数据库层有 CHECK 约束）。</summary>
    public int EndPeriod { get; set; }

    /// <summary>起始周，取值 1~25。</summary>
    public int StartWeek { get; set; }

    /// <summary>结束周，取值 1~25，必须大于等于起始周。</summary>
    public int EndWeek { get; set; }

    /// <summary>
    /// 周次范围的原始文本，如 1-16。
    /// 【已废弃】仅为历史数据/展示保留：新代码禁止读取它做任何时间计算，
    /// 一律使用 StartWeek 与 EndWeek（既有的 1-16 之类文本已在 Add_Week_Fields 迁移中回填）。
    /// </summary>
    public string WeekRange { get; set; } = string.Empty;

    /// <summary>
    /// 单双周：0 每周 / 1 单周 / 2 双周（见 <see cref="WeekParityTypes"/>）。
    /// 以学期教学周序号为准，第 1、3、5… 周为单周；周序号经 ISemesterService 换算。
    /// </summary>
    public int WeekParity { get; set; }

    /// <summary>上课地点。</summary>
    public string Location { get; set; } = string.Empty;
}
