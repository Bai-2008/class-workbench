namespace ClassScheduleApp.Repositories;

/// <summary>
/// 课程维护（列表 / 新增 / 编辑 / 编号查重）的数据访问接口。
/// </summary>
public interface ICourseAdminRepository
{
    /// <summary>获取全部课程及其排课时间，按课程编号升序。</summary>
    Task<IReadOnlyList<CourseDetail>> GetAllCoursesAsync(CancellationToken cancellationToken = default);

    /// <summary>课程编号是否已被占用；excludeCourseId 用于编辑时排除自己。</summary>
    Task<bool> IsCourseNoTakenAsync(string courseNo, int? excludeCourseId = null, CancellationToken cancellationToken = default);

    /// <summary>新增课程及其排课时间。</summary>
    Task<OperationResult> AddCourseAsync(CourseDetail course, CancellationToken cancellationToken = default);

    /// <summary>更新课程及其排课时间（该课程的第一条排课；没有则补一条）。</summary>
    Task<OperationResult> UpdateCourseAsync(CourseDetail course, CancellationToken cancellationToken = default);
    /// <summary>
    /// 新增课程（课程管理用）：课程名必填、至少一条时段；**不关联任何学生**。
    /// 同名同师且时段集合完全一致 → 返回失败并提示「已存在」；时段不同 → 允许并存（开课班判定）。
    /// </summary>
    Task<OperationResult> AddCourseCheckedAsync(CourseDetail course, CancellationToken cancellationToken = default);

    /// <summary>删除课程前的影响面：关联学生数、考勤记录数（保留）、涉及该课程的调整数。</summary>
    Task<CourseDeleteImpact> GetDeleteImpactAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除课程：连带删除其全部时段、全部选课关联、涉及该课程的调整记录；
    /// **考勤记录保留**（历史靠课程名快照呈现）。一次事务。
    /// </summary>
    Task<OperationResult> DeleteCourseAsync(int courseId, CancellationToken cancellationToken = default);

    /// <summary>清空某生某学期的课表：仅解除该生全部课程关联，课程与时段保留。</summary>
    Task<OperationResult> ClearStudentScheduleAsync(int studentId, string semester, CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空所有人的课表（学期清空）：删除全部选课关联、全部课程（含时段）、全部调整记录；
    /// **学生与考勤记录保留**。一次事务。
    /// </summary>
    Task<OperationResult> ClearAllSchedulesAsync(CancellationToken cancellationToken = default);
    /// <summary>「清空所有人的课表」确认框所需的全局计数（将删三项 + 保留两项）。</summary>
    Task<ScheduleClearImpact> GetClearAllImpactAsync(CancellationToken cancellationToken = default);
}