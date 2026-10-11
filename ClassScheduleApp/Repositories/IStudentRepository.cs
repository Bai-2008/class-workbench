using ClassScheduleApp.Models;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 学生与课表的数据访问接口。
/// 所有 LINQ 查询只允许出现在实现层（Repositories），不得返回 IQueryable。
/// </summary>
public interface IStudentRepository
{
    /// <summary>获取全部学生（含学号、姓名、班级），按学号升序。</summary>
    Task<IReadOnlyList<Student>> GetAllStudentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定学生的课表：课程名、星期、起止节次、教室、授课老师，
    /// 按星期排序、同一天再按起始节次排序。
    /// </summary>
    Task<IReadOnlyList<StudentScheduleItem>> GetStudentScheduleAsync(int studentId, CancellationToken cancellationToken = default);
}
