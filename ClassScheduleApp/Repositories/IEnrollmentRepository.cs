namespace ClassScheduleApp.Repositories;

/// <summary>
/// 选课关系的数据访问接口（多对多中间表 StudentCourses）。
/// </summary>
public interface IEnrollmentRepository
{
    /// <summary>获取该学生在指定学期已选的课程 Id。</summary>
    Task<IReadOnlyList<int>> GetEnrolledCourseIdsAsync(int studentId, string semester, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按差异保存选课：新勾选的插入、取消勾选的删除，未变化的保持不动。
    /// 重复选课由 (StudentId, CourseId, Semester) 唯一约束兜底，冲突时返回友好提示。
    /// </summary>
    Task<OperationResult> SaveEnrollmentAsync(int studentId, IReadOnlyCollection<int> selectedCourseIds, string semester, CancellationToken cancellationToken = default);
}
