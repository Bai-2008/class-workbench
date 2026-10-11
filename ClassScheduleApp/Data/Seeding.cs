using ClassScheduleApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Data;

/// <summary>
/// 种子数据：程序首次启动且库为空时写入 5 名学生、6 门课程（含排课时间）以及选课关系。
/// 库中已有数据时直接跳过，不覆盖也不重复写入。
/// </summary>
public static class Seeding
{
    /// <summary>统一学期。</summary>
    public const string Semester = "2025-2026-1";

    /// <summary>确保数据库已建库建表，并在库为空时写入种子数据。</summary>
    public static async Task EnsureSeededAsync(IDbContextFactory<AppDbContext> factory, CancellationToken cancellationToken = default)
    {
        await using var ctx = await factory.CreateDbContextAsync(cancellationToken);

        // 结构与迁移保持一致（首次在新环境运行时自动建库）
        await ctx.Database.MigrateAsync(cancellationToken);

        var hasData = await ctx.Students.AnyAsync(cancellationToken)
                      || await ctx.Courses.AnyAsync(cancellationToken);
        if (hasData)
        {
            return;
        }

        // 6 门课程，每门带一条排课时间（星期、起止节次、周次、教室）
        var math = NewCourse("MA101", "高等数学", "王老师", 4, dayOfWeek: 1, startPeriod: 1, endPeriod: 2, location: "3-201");
        var english = NewCourse("EN101", "大学英语", "刘老师", 3, dayOfWeek: 1, startPeriod: 3, endPeriod: 4, location: "1-105");
        var operatingSystem = NewCourse("CS202", "操作系统", "周老师", 3.5, dayOfWeek: 2, startPeriod: 3, endPeriod: 4, location: "3-102");
        var dataStructure = NewCourse("CS201", "数据结构", "陈老师", 4, dayOfWeek: 3, startPeriod: 5, endPeriod: 6, location: "2-310");
        var linearAlgebra = NewCourse("MA102", "线性代数", "郑老师", 3, dayOfWeek: 4, startPeriod: 1, endPeriod: 2, location: "2-203");
        var physicalEducation = NewCourse("PE101", "体育", "吴老师", 1, dayOfWeek: 5, startPeriod: 7, endPeriod: 8, location: "操场");

        // 5 名学生
        var zhangSan = NewStudent("2024001", "张三", "男", "计算机2401", "13800000001");
        var liSi = NewStudent("2024002", "李四", "女", "计算机2401", "13800000002");
        var wangWu = NewStudent("2024003", "王五", "男", "计算机2402", "13800000003");
        var zhaoLiu = NewStudent("2024004", "赵六", "女", "计算机2402", "13800000004");
        var sunQi = NewStudent("2024005", "孙七", "男", "计算机2403", "13800000005");

        ctx.Students.AddRange(zhangSan, liSi, wangWu, zhaoLiu, sunQi);
        ctx.Courses.AddRange(math, english, operatingSystem, dataStructure, linearAlgebra, physicalEducation);

        // 每人选 3–5 门课
        Enroll(ctx, zhangSan, math, english, dataStructure, operatingSystem);
        Enroll(ctx, liSi, math, english, dataStructure, linearAlgebra, physicalEducation);
        Enroll(ctx, wangWu, dataStructure, operatingSystem, linearAlgebra);
        Enroll(ctx, zhaoLiu, math, english, linearAlgebra, physicalEducation);
        Enroll(ctx, sunQi, math, dataStructure, operatingSystem, linearAlgebra, physicalEducation);

        await ctx.SaveChangesAsync(cancellationToken);
    }

    private static Student NewStudent(string studentNo, string name, string gender, string className, string phone)
    {
        return new Student
        {
            StudentNo = studentNo,
            Name = name,
            Gender = gender,
            ClassName = className,
            Phone = phone,
        };
    }

    private static Course NewCourse(string courseNo, string courseName, string teacher, double credit, int dayOfWeek, int startPeriod, int endPeriod, string location)
    {
        var course = new Course
        {
            CourseNo = courseNo,
            CourseName = courseName,
            Teacher = teacher,
            Credit = credit,
        };

        course.CourseSchedules.Add(new CourseSchedule
        {
            DayOfWeek = dayOfWeek,
            StartPeriod = startPeriod,
            EndPeriod = endPeriod,
            StartWeek = 1,
            EndWeek = 16,
            WeekRange = "1-16",
            Location = location,
        });

        return course;
    }

    /// <summary>建立选课关系（通过导航属性，外键由 EF 按 Id 自动填充）。</summary>
    private static void Enroll(AppDbContext ctx, Student student, params Course[] courses)
    {
        foreach (var course in courses)
        {
            ctx.StudentCourses.Add(new StudentCourse
            {
                Student = student,
                Course = course,
                Semester = Semester,
            });
        }
    }
}
