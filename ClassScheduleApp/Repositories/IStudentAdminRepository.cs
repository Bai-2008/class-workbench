using ClassScheduleApp.Models;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 学生维护（新增 / 编辑 / 删除 / 学号查重）的数据访问接口。
/// 为避免修改既有公共接口 IStudentRepository，新增能力放在这个新接口里。
/// </summary>
public interface IStudentAdminRepository
{
    /// <summary>学号是否已被占用；excludeStudentId 用于编辑时排除自己。</summary>
    Task<bool> IsStudentNoTakenAsync(string studentNo, int? excludeStudentId = null, CancellationToken cancellationToken = default);

    /// <summary>新增学生。</summary>
    Task<OperationResult> AddStudentAsync(Student student, CancellationToken cancellationToken = default);

    /// <summary>更新学生。</summary>
    Task<OperationResult> UpdateStudentAsync(Student student, CancellationToken cancellationToken = default);

    /// <summary>删除学生，其选课记录由数据库外键的 ON DELETE CASCADE 一并删除。</summary>
    Task<OperationResult> DeleteStudentAsync(int studentId, CancellationToken cancellationToken = default);

    /// <summary>取全部学号（批量导入判重用；纯增量导入以学号为身份键）。</summary>
    Task<IReadOnlyList<string>> GetAllStudentNosAsync(CancellationToken cancellationToken = default);

    /// <summary>批量新增学生（一次事务；学号重复会被数据库唯一约束拦下）。</summary>
    Task<OperationResult> AddRangeAsync(IReadOnlyList<Models.Student> students, CancellationToken cancellationToken = default);

    /// <summary>批量删除前的影响面：学生数、选课关联数、考勤条数（保留）、综测条数（保留）。</summary>
    Task<StudentDeleteImpact> GetDeleteRangeImpactAsync(IReadOnlyList<int> studentIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除学生（一次事务）：先对该批学生缺失的考勤 / 综测姓名快照兜底回填，再删除学生；
    /// 选课关联随学生级联删除，考勤与综测记录保留（外键置空，历史仍显示姓名）。
    /// </summary>
    Task<OperationResult> DeleteRangeAsync(IReadOnlyList<int> studentIds, CancellationToken cancellationToken = default);
}
