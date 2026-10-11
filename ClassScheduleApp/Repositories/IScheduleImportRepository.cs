using ClassScheduleApp.Models;
using ClassScheduleApp.Services.ScheduleImport;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 一次「覆盖式导入」对某位学生的执行结果（供导入报告汇总）。
/// </summary>
public sealed class ScheduleImportOutcome
{
    /// <summary>本次新建的课程（课程名 + 教师 + 时段摘要）。</summary>
    public List<string> CreatedCourses { get; } = new();

    /// <summary>复用既有课程（时段集合完全一致）的条数。</summary>
    public int LinkedExistingCount { get; set; }

    /// <summary>覆盖后因零关联被删除的课程（课程名 + 教师）。</summary>
    public List<string> DeletedOrphanCourses { get; } = new();

    /// <summary>
    /// 开课班拆分：同名同师但时段集合不一致而新建的课程，
    /// 逐条给出「新建时段 vs 既有时段」对比（时段有交集时附人工核对提示）。
    /// </summary>
    public List<string> CourseSplits { get; } = new();
}

/// <summary>
/// 课表导入的数据访问接口：学生匹配 + 按导入内容覆盖某生某学期的选课关系。
/// </summary>
public interface IScheduleImportRepository
{
    /// <summary>按姓名匹配学生（可能重名，返回全部候选）。</summary>
    Task<IReadOnlyList<Student>> FindStudentsByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>按学号取学生；不存在返回 null。</summary>
    Task<Student?> GetStudentByNoAsync(string studentNo, CancellationToken cancellationToken = default);

    /// <summary>取全部学生（按学号升序），供手动绑定下拉使用。</summary>
    Task<IReadOnlyList<Student>> GetAllStudentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 覆盖式导入：解除该生本学期全部课程关联后按导入内容重建；每文件一个事务。
    ///
    /// 课程同一性判定（与导入顺序无关）：
    /// 课程名 + 教师 + **时段集合完全一致**（星期/起止节次/起止周/单双周逐项相等，与顺序无关）
    ///   · 完全一致 → 复用该课程，仅建立学生关联；
    ///   · 名师相同但时段集合不同 → 视为**不同开课班**，按导入时段新建课程并关联；
    ///   · 时段有交集但集合不等 → 同样新建，并在报告中附「时段部分重叠，请人工核对」。
    /// **绝不增删既有课程的时段。**
    /// </summary>
    Task<ScheduleImportOutcome> ApplyStudentScheduleAsync(
        int studentId,
        string semester,
        IReadOnlyList<ParsedScheduleEntry> entries,
        CancellationToken cancellationToken = default);
}
