using System.Windows;
using ClassScheduleApp.Data;
using ClassScheduleApp.Repositories;
using ClassScheduleApp.Services;
using ClassScheduleApp.Services.ScheduleImport;
using ClassScheduleApp.ViewModels;
using ClassScheduleApp.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClassScheduleApp;

/// <summary>
/// 应用入口（组合根）：构建 ServiceProvider、写入种子数据、装配主窗口与 ViewModel。
/// 本项目唯一的依赖注册处，新增 Repository 时只在此追加一行注册。
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 模块注册必须在解析任何 ViewModel 之前完成：
        // 各模块的 ViewModel 类型由这里（组合根）注入，Services 层因此不引用 ViewModels / Views 层。
        FeatureRegistry.Register(
            typeof(StudentScheduleViewModel),
            typeof(SettingsViewModel),
            typeof(FreeTimeStatsViewModel),
            typeof(AttendanceViewModel),
            typeof(ClassFundsViewModel),
            typeof(ScoreEntryViewModel),
            typeof(ScheduleAdjustmentViewModel),
            typeof(ScheduleImportViewModel));

        var services = new ServiceCollection();
        RegisterServices(services);
        _serviceProvider = services.BuildServiceProvider();

        try
        {
            var factory = _serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            var viewModel = _serviceProvider.GetRequiredService<MainViewModel>();
            var studentScheduleViewModel = _serviceProvider.GetRequiredService<StudentScheduleViewModel>();

            // 主题必须早于窗口创建：窗口在 Loaded 时按已生效的主题画底色
            var settings = _serviceProvider.GetRequiredService<ISettingsService>();
            var themeService = _serviceProvider.GetRequiredService<IThemeService>();
            themeService.Apply(settings.GetThemeMode());

            // 每日自动备份：当日首次启动触发一次（当日已有 daily 备份则跳过）；
            // 失败不抛出，避免备份问题阻断应用启动。
            var backupService = _serviceProvider.GetRequiredService<IBackupService>();
            backupService.EnsureDailyBackup();

            // 在后台线程完成种子数据与首屏数据加载：
            // 一是避免异步续体抢在窗口创建之前执行导致应用直接退出，
            // 二是窗口显示时数据已就绪、不出现空列表闪烁。
            Task.Run(async () =>
            {
                await Seeding.EnsureSeededAsync(factory);
                await studentScheduleViewModel.InitializeAsync();
            }).GetAwaiter().GetResult();

            var window = new MainWindow { DataContext = viewModel };
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"数据库初始化失败：{ex.Message}",
                "启动失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    /// <summary>注册全部服务（新增 Repository 时只在此追加一行）。</summary>
    private static void RegisterServices(IServiceCollection services)
    {
        // DbContext 一律通过工厂短生命周期创建，禁止 AddDbContext 长生命周期持有
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite(DatabasePathProvider.GetConnectionString()));

        services.AddSingleton<IStudentRepository, StudentRepository>();
        services.AddSingleton<IStudentAdminRepository, StudentAdminRepository>();
        services.AddSingleton<ICourseAdminRepository, CourseAdminRepository>();
        services.AddSingleton<IEnrollmentRepository, EnrollmentRepository>();
        services.AddSingleton<IFreeTimeRepository, FreeTimeRepository>();
        services.AddSingleton<IAttendanceRepository, AttendanceRepository>();
        services.AddSingleton<IExpenseRepository, ExpenseRepository>();
        services.AddSingleton<IScoreEntryRepository, ScoreEntryRepository>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IExportService, ClosedXmlExportService>();
        services.AddSingleton<IAttendanceExportService, AttendanceExportService>();
        services.AddSingleton<IExpenseExportService, ExpenseExportService>();
        services.AddSingleton<IScoreExportService, ScoreExportService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<ISemesterSettingRepository, SemesterSettingRepository>();
        services.AddSingleton<ISemesterService, SemesterService>();
        services.AddSingleton<IPeriodTimeRepository, PeriodTimeRepository>();
        services.AddSingleton<IPeriodTimeService, PeriodTimeService>();        services.AddSingleton<IScheduleAdjustmentRepository, ScheduleAdjustmentRepository>();
        services.AddSingleton<IScheduleAdjustmentService, ScheduleAdjustmentService>();
        services.AddSingleton<IStudentImportService, StudentImportService>();
        services.AddSingleton<IScheduleDayCalculator, ScheduleDayCalculator>();
        services.AddSingleton<IScheduleImportRepository, ScheduleImportRepository>();
        services.AddSingleton<IScheduleTextParser, ScheduleTextParser>();
        services.AddSingleton<IScheduleFileReader, ScheduleFileReader>();
        services.AddSingleton<IScheduleImportService, ScheduleImportService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<StudentScheduleViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<FreeTimeStatsViewModel>();
        services.AddSingleton<AttendanceViewModel>();
        services.AddSingleton<ClassFundsViewModel>();
        services.AddSingleton<ScoreEntryViewModel>();
        services.AddSingleton<ScheduleAdjustmentViewModel>();
        services.AddSingleton<ScheduleImportViewModel>();

        // 对话框 ViewModel：每次打开都必须是新实例
        services.AddTransient<StudentEditViewModel>();
        services.AddTransient<CourseManageViewModel>();
        services.AddTransient<CourseEditViewModel>();
        services.AddTransient<EnrollmentViewModel>();
    }
}
