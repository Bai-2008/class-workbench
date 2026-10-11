namespace ClassScheduleApp.Services;

/// <summary>
/// 功能模块注册表：集中定义系统里有哪些模块、默认可见性与导航顺序。
/// 模块的 ViewModel 类型由组合根传入，Services 层因此不引用 ViewModels / Views 层。
/// </summary>
public static class FeatureRegistry
{
    /// <summary>学生课表模块 Id。</summary>
    public const string StudentScheduleId = "StudentSchedule";

    /// <summary>设置模块 Id。</summary>
    public const string SettingsId = "Settings";

    /// <summary>空闲时间统计模块 Id。</summary>
    public const string FreeTimeStatsId = "FreeTimeStats";

    /// <summary>考勤管理模块 Id。</summary>
    public const string AttendanceId = "Attendance";

    /// <summary>班费记账模块 Id。</summary>
    public const string ClassFundsId = "ClassFunds";

    /// <summary>综测记录模块 Id。</summary>
    public const string ScoreEntryId = "ScoreEntry";

    /// <summary>调休补课模块 Id。</summary>
    public const string ScheduleAdjustmentId = "ScheduleAdjustment";

    /// <summary>课表导入模块 Id。</summary>
    public const string ScheduleImportId = "ScheduleImport";

    /// <summary>导入（下箭头入托盘）类图标路径。</summary>
    private const string ImportIconPath =
        "M12,3 V15 M7,10 L12,15 L17,10 M4,17 V20 H20 V17";

    /// <summary>星标（综测）类图标路径。</summary>
    private const string StarIconPath =
        "M12,3.5 L14.5,9 L20.5,9.6 L16,13.6 L17.4,19.5 L12,16.4 L6.6,19.5 L8,13.6 L3.5,9.6 L9.5,9 Z";

    /// <summary>钱袋类图标路径。</summary>
    private const string WalletIconPath =
        "M3,7 H20 V19 H3 Z M3,7 L15,3.5 L16,7 M16.5,13 A1.5,1.5 0 1 0 16.5,13.01";

    /// <summary>考勤（对勾）类图标路径。</summary>
    private const string AttendanceIconPath =
        "M4,6 H16 V20 H4 Z M4,10 H16 M7,3 V7 M13,3 V7 M9,14 L11,16.5 L15,12";

    /// <summary>柱状统计类图标路径。</summary>
    private const string ChartIconPath =
        "M4,20 V11 M9,20 V5 M14,20 V14 M19,20 V8 M3,22 H21";

    /// <summary>日历类图标路径。</summary>
    private const string CalendarIconPath =
        "M3,6 H21 V20 H3 Z M3,10.5 H21 M8,3.5 V7.5 M16,3.5 V7.5";

    /// <summary>齿轮类图标路径。</summary>
    private const string GearIconPath =
        "M12,4 V7 M12,17 V20 M4,12 H7 M17,12 H20 M6.6,6.6 L8.7,8.7 M15.3,15.3 L17.4,17.4 " +
        "M6.6,17.4 L8.7,15.3 M15.3,8.7 L17.4,6.6 M12,9.5 A2.5,2.5 0 1 0 12,14.5 A2.5,2.5 0 1 0 12,9.5";

    private static readonly List<FeatureModule> RegisteredModules = new();

    /// <summary>已注册的模块（按导航栏顺序）。</summary>
    public static IReadOnlyList<FeatureModule> Modules => RegisteredModules;

    /// <summary>
    /// 由组合根在启动时调用一次：注册模块并注入各自的 ViewModel 类型。
    /// 新增模块时：这里加一行模块定义 + 一个类型参数，再在组合根传入类型、在全局模板资源里加一条隐式 DataTemplate。
    /// </summary>
    public static void Register(
        Type studentScheduleViewModelType,
        Type settingsViewModelType,
        Type freeTimeStatsViewModelType,
        Type attendanceViewModelType,
        Type classFundsViewModelType,
        Type scoreEntryViewModelType,
        Type scheduleAdjustmentViewModelType,
        Type scheduleImportViewModelType)
    {
        RegisteredModules.Clear();

        RegisteredModules.Add(new FeatureModule(
            StudentScheduleId,
            "学生课表",
            CalendarIconPath,
            studentScheduleViewModelType,
            isVisibleByDefault: true));

        RegisteredModules.Add(new FeatureModule(
            FreeTimeStatsId,
            "空闲时间统计",
            ChartIconPath,
            freeTimeStatsViewModelType,
            isVisibleByDefault: true));

        RegisteredModules.Add(new FeatureModule(
            ScheduleAdjustmentId,
            "调休补课",
            CalendarIconPath,
            scheduleAdjustmentViewModelType,
            isVisibleByDefault: true));

        RegisteredModules.Add(new FeatureModule(
            AttendanceId,
            "考勤管理",
            AttendanceIconPath,
            attendanceViewModelType,
            isVisibleByDefault: true));

        RegisteredModules.Add(new FeatureModule(
            ClassFundsId,
            "班费记账",
            WalletIconPath,
            classFundsViewModelType,
            isVisibleByDefault: true));

        RegisteredModules.Add(new FeatureModule(
            ScoreEntryId,
            "综测记录",
            StarIconPath,
            scoreEntryViewModelType,
            isVisibleByDefault: true));

        // 设置固定可见、不受开关控制，并固定排在导航栏最底部
        RegisteredModules.Add(new FeatureModule(
            ScheduleImportId,
            "课表导入",
            ImportIconPath,
            scheduleImportViewModelType,
            isVisibleByDefault: true));

        RegisteredModules.Add(new FeatureModule(
            SettingsId,
            "设置",
            GearIconPath,
            settingsViewModelType,
            isVisibleByDefault: true,
            isAlwaysVisible: true));
    }
}
