using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services;

/// <summary>
/// 考勤导出服务。
/// </summary>
public interface IAttendanceExportService
{
    /// <summary>
    /// 导出到 data/exports/考勤统计_{start}_{end}.xlsx：
    /// Sheet1「按学生汇总」（列同界面）、Sheet2「明细记录」（日期/学生/课程/状态/备注）。
    /// </summary>
    Task<ExportResult> ExportAttendanceAsync(
        DateOnly start,
        DateOnly end,
        IReadOnlyList<StudentAttendanceStats> studentStats,
        IReadOnlyList<AttendanceRecordInfo> records,
        CancellationToken cancellationToken = default);
}
