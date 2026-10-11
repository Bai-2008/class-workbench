using ClassScheduleApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Data;

/// <summary>
/// 应用数据库上下文，管理 Students / Courses / CourseSchedules / StudentCourses 四张表。
/// 表结构的任何变更都必须通过 EF Core Migration 落地，禁止直接修改 .db 文件。
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <summary>学生表。</summary>
    public DbSet<Student> Students => Set<Student>();

    /// <summary>课程表。</summary>
    public DbSet<Course> Courses => Set<Course>();

    /// <summary>排课表。</summary>
    public DbSet<CourseSchedule> CourseSchedules => Set<CourseSchedule>();

    /// <summary>学生选课中间表（多对多）。</summary>
    public DbSet<StudentCourse> StudentCourses => Set<StudentCourse>();

    /// <summary>考勤表。</summary>
    public DbSet<Attendance> Attendances => Set<Attendance>();

    /// <summary>班费流水表。</summary>
    public DbSet<Expense> Expenses => Set<Expense>();

    /// <summary>综测记录表。</summary>
    public DbSet<ScoreEntry> ScoreEntries => Set<ScoreEntry>();

    /// <summary>学期设置表。</summary>
    public DbSet<SemesterSetting> SemesterSettings => Set<SemesterSetting>();

    /// <summary>作息时间表（每日各节起止时刻）。</summary>
    public DbSet<PeriodTime> PeriodTimes => Set<PeriodTime>();

    /// <summary>调休补课调整表。</summary>
    public DbSet<ScheduleAdjustment> ScheduleAdjustments => Set<ScheduleAdjustment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1. Students：StudentNo 唯一
        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.StudentNo).IsRequired();
            entity.HasIndex(s => s.StudentNo).IsUnique();
            entity.Property(s => s.Name).IsRequired();
            entity.Property(s => s.Gender).IsRequired();
            entity.Property(s => s.ClassName).IsRequired();
            entity.Property(s => s.Phone);
            entity.Property(s => s.Note);
        });

        // 2. Courses：CourseNo 选填但唯一（多个 NULL 允许）；Credit 选填
        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("Courses");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.CourseNo);
            entity.HasIndex(c => c.CourseNo).IsUnique();
            entity.Property(c => c.CourseName).IsRequired();
            entity.Property(c => c.Teacher).IsRequired();
            entity.Property(c => c.Credit);
        });

        // 3. CourseSchedules：外键关联 Courses，级联删除；DayOfWeek 取 1–7 且 EndPeriod >= StartPeriod
        modelBuilder.Entity<CourseSchedule>(entity =>
        {
            entity.ToTable("CourseSchedules", table =>
            {
                table.HasCheckConstraint("CK_CourseSchedules_DayOfWeek", "\"DayOfWeek\" BETWEEN 1 AND 7");
                table.HasCheckConstraint("CK_CourseSchedules_Period", "\"EndPeriod\" >= \"StartPeriod\"");
            });
            entity.HasKey(cs => cs.Id);
            entity.Property(cs => cs.WeekRange).IsRequired();
            // 单双周：非空、默认 0（每周）。带默认值可让迁移走 SQLite 的 ADD COLUMN，避免重建表。
            entity.Property(cs => cs.WeekParity).IsRequired().HasDefaultValue(Models.WeekParityTypes.Every);
            entity.Property(cs => cs.Location).IsRequired();
            entity.HasOne(cs => cs.Course)
                  .WithMany(c => c.CourseSchedules)
                  .HasForeignKey(cs => cs.CourseId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 4. StudentCourses：多对多中间表，(StudentId, CourseId, Semester) 三元组唯一；两个外键均级联删除
        modelBuilder.Entity<StudentCourse>(entity =>
        {
            entity.ToTable("StudentCourses");
            entity.HasKey(sc => sc.Id);
            entity.HasIndex(sc => new { sc.StudentId, sc.CourseId, sc.Semester }).IsUnique();
            entity.Property(sc => sc.Semester).IsRequired();
            entity.HasOne(sc => sc.Student)
                  .WithMany(s => s.StudentCourses)
                  .HasForeignKey(sc => sc.StudentId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(sc => sc.Course)
                  .WithMany(c => c.StudentCourses)
                  .HasForeignKey(sc => sc.CourseId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 5. Attendances：StudentId 删除时置空（SET NULL）+ 学生姓名快照；CourseId 可空外键（null = 班级活动），删除课程时置空
        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.ToTable("Attendances");
            entity.HasKey(a => a.Id);

            // EF Core 8 的 SQLite provider 默认把 DateOnly 存为 TEXT（yyyy-MM-dd）
            entity.Property(a => a.Date).HasColumnType("TEXT").IsRequired();
            entity.Property(a => a.Status).IsRequired();
            entity.Property(a => a.Note);

            // 学生姓名快照：学生被删除后历史考勤仍可读（与 CourseName 同语义）
            entity.Property(a => a.StudentName);

            // 查询模式：(日期区间) 与 (日期, 场合)
            entity.HasIndex(a => a.Date);
            entity.HasIndex(a => new { a.Date, a.CourseId });

            entity.HasOne(a => a.Student)
                  .WithMany()
                  .HasForeignKey(a => a.StudentId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.Course)
                  .WithMany()
                  .HasForeignKey(a => a.CourseId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 6. Expenses：金额必须大于 0；无外键；余额不落库
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("Expenses", table =>
            {
                table.HasCheckConstraint("CK_Expenses_Amount", "\"Amount\" > 0");
            });
            entity.HasKey(e => e.Id);

            // EF Core 8 的 SQLite provider 把 decimal 存为 TEXT（保持精度），聚合在内存里做
            entity.Property(e => e.Amount).HasColumnType("TEXT").IsRequired();
            entity.Property(e => e.Date).HasColumnType("TEXT").IsRequired();
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Category).IsRequired();
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Handler).IsRequired();
            entity.Property(e => e.Note);

            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => e.Category);
        });

        // 7. ScoreEntries：StudentId 删除时置空（SET NULL）+ 学生姓名快照；分数可为负；无唯一约束
        modelBuilder.Entity<ScoreEntry>(entity =>
        {
            entity.ToTable("ScoreEntries");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Score).HasColumnType("TEXT").IsRequired();
            entity.Property(e => e.EntryDate).HasColumnType("TEXT").IsRequired();
            entity.Property(e => e.Semester).IsRequired();
            entity.Property(e => e.Category).IsRequired();
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Evidence);
            entity.Property(e => e.Operator).IsRequired();

            // 学生姓名快照：学生被删除后历史综测仍可读（与考勤 CourseName 同语义）
            entity.Property(e => e.StudentName);

            entity.HasIndex(e => e.Semester);
            entity.HasIndex(e => new { e.Semester, e.StudentId });

            entity.HasOne(e => e.Student)
                  .WithMany()
                  .HasForeignKey(e => e.StudentId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 8. SemesterSettings：学期名唯一；起始日可空（未配置时调用方降级）
        modelBuilder.Entity<SemesterSetting>(entity =>
        {
            entity.ToTable("SemesterSettings");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.SemesterName).IsRequired();
            entity.HasIndex(s => s.SemesterName).IsUnique();
            entity.Property(s => s.StartDate).HasColumnType("TEXT");
            entity.Property(s => s.WeekCount).IsRequired();
        });

        // 9. PeriodTimes：节次号唯一；12 节作息以 HasData 随迁移写入种子（设置页可改）
        modelBuilder.Entity<PeriodTime>(entity =>
        {
            entity.ToTable("PeriodTimes");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.PeriodNo).IsRequired();
            entity.HasIndex(p => p.PeriodNo).IsUnique();
            entity.Property(p => p.StartTime).HasColumnType("TEXT").IsRequired();
            entity.Property(p => p.EndTime).HasColumnType("TEXT").IsRequired();

            entity.HasData(
                new PeriodTime { Id = 1, PeriodNo = 1, StartTime = new TimeOnly(8, 30), EndTime = new TimeOnly(9, 15) },
                new PeriodTime { Id = 2, PeriodNo = 2, StartTime = new TimeOnly(9, 20), EndTime = new TimeOnly(10, 5) },
                new PeriodTime { Id = 3, PeriodNo = 3, StartTime = new TimeOnly(10, 25), EndTime = new TimeOnly(11, 10) },
                new PeriodTime { Id = 4, PeriodNo = 4, StartTime = new TimeOnly(11, 15), EndTime = new TimeOnly(12, 0) },
                new PeriodTime { Id = 5, PeriodNo = 5, StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(14, 45) },
                new PeriodTime { Id = 6, PeriodNo = 6, StartTime = new TimeOnly(14, 50), EndTime = new TimeOnly(15, 35) },
                new PeriodTime { Id = 7, PeriodNo = 7, StartTime = new TimeOnly(15, 55), EndTime = new TimeOnly(16, 40) },
                new PeriodTime { Id = 8, PeriodNo = 8, StartTime = new TimeOnly(16, 45), EndTime = new TimeOnly(17, 30) },
                new PeriodTime { Id = 9, PeriodNo = 9, StartTime = new TimeOnly(18, 30), EndTime = new TimeOnly(19, 15) },
                new PeriodTime { Id = 10, PeriodNo = 10, StartTime = new TimeOnly(19, 20), EndTime = new TimeOnly(20, 5) },
                new PeriodTime { Id = 11, PeriodNo = 11, StartTime = new TimeOnly(20, 10), EndTime = new TimeOnly(20, 55) },
                new PeriodTime { Id = 12, PeriodNo = 12, StartTime = new TimeOnly(21, 0), EndTime = new TimeOnly(21, 45) });
        });

        // 10. ScheduleAdjustments：按学期 + 日期查询；CourseId 指向 Courses（不设级联，避免误删课程时连带删调整）
        modelBuilder.Entity<ScheduleAdjustment>(entity =>
        {
            entity.ToTable("ScheduleAdjustments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Semester).IsRequired();
            entity.Property(a => a.AdjustmentType).IsRequired();
            entity.Property(a => a.AffectedDate).HasColumnType("TEXT").IsRequired();
            entity.Property(a => a.CreatedAt).IsRequired();
            entity.HasIndex(a => new { a.Semester, a.AffectedDate });
            entity.HasIndex(a => a.CourseId);
        });
    }
}
