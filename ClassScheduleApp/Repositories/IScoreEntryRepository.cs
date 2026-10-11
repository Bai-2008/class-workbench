namespace ClassScheduleApp.Repositories;

/// <summary>
/// 综测记录的数据访问接口。
/// 学期取值与 StudentCourses.Semester 一致（学期列表复用空闲时间统计的 IFreeTimeRepository.GetSemestersAsync）。
/// </summary>
public interface IScoreEntryRepository
{
    /// <summary>
    /// 查询综测记录明细；学期 / 学生 / 类别 / 日期范围均为可选过滤（传 null 表示不过滤）。
    /// 结果按记录日期升序、同日期按 Id 升序。
    /// </summary>
    Task<IReadOnlyList<ScoreEntryInfo>> GetEntriesAsync(
        string? semester = null,
        int? studentId = null,
        string? category = null,
        DateOnly? rangeStart = null,
        DateOnly? rangeEnd = null,
        CancellationToken cancellationToken = default);

    /// <summary>事务内为多个学生批量插入同一条记录内容。</summary>
    Task<OperationResult> AddBatchAsync(
        IReadOnlyList<int> studentIds,
        ScoreEntryDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>修改指定记录（学生不变）。</summary>
    Task<OperationResult> UpdateAsync(
        int id,
        ScoreEntryDraft draft,
        CancellationToken cancellationToken = default);

    /// <summary>删除指定记录。</summary>
    Task<OperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 某学期的学生汇总：每学生各预设类别得分合计 + 总分，按总分降序（同分按学号升序）。
    /// 没有记录的学生也会返回（各类别为 0）。
    /// </summary>
    Task<IReadOnlyList<StudentScoreSummary>> GetSummaryAsync(
        string semester,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在指定学期的这些学生里，找出已经存在完全相同 (类别, 分数, 说明) 记录的学生 Id（用于重复提示）。
    /// </summary>
    Task<IReadOnlyList<int>> FindDuplicateStudentIdsAsync(
        string semester,
        IReadOnlyList<int> studentIds,
        string category,
        decimal score,
        string description,
        CancellationToken cancellationToken = default);

    /// <summary>类别下拉数据：预设类别在前，历史里出现过的自定义类别在后。</summary>
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);
}
