using ClassScheduleApp.Data;
using ClassScheduleApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 学生与课表的数据访问实现。
/// 按规范注入 IDbContextFactory，每个方法内部创建并释放短生命周期 DbContext，
/// 禁止把 DbContext 存为字段跨方法复用。
/// </summary>
public sealed class StudentRepository : IStudentRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public StudentRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Student>> GetAllStudentsAsync(CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        return await ctx.Students
            .AsNoTracking()
            .OrderBy(s => s.StudentNo)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StudentScheduleItem>> GetStudentScheduleAsync(int studentId, CancellationToken cancellationToken = default)
    {
        using var ctx = _factory.CreateDbContext();

        // StudentCourses → CourseSchedules → Courses 三表 JOIN，
        // 外键全部用 Id 关联，不出现任何文字字段关联。
        var query =
            from sc in ctx.StudentCourses.AsNoTracking()
            where sc.StudentId == studentId
            join cs in ctx.CourseSchedules.AsNoTracking() on sc.CourseId equals cs.CourseId
            join c in ctx.Courses.AsNoTracking() on cs.CourseId equals c.Id
            select new StudentScheduleItem
            {
                CourseId = c.Id,
                CourseName = c.CourseName,
                Teacher = c.Teacher,
                DayOfWeek = cs.DayOfWeek,
                StartPeriod = cs.StartPeriod,
                EndPeriod = cs.EndPeriod,
                Location = cs.Location,
                StartWeek = cs.StartWeek,
                EndWeek = cs.EndWeek,
                WeekParity = cs.WeekParity,
            };

        return await query
            .OrderBy(item => item.DayOfWeek)
            .ThenBy(item => item.StartPeriod)
            .ToListAsync(cancellationToken);
    }
}
