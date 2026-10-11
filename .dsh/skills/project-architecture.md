---
name: project-architecture
description: 班级学生课表管理应用的工程结构与数据模型规范。当需要新增实体或新表、写 EF 迁移的实体侧改动、新增/修改学生、课程、排课、选课、考勤相关功能，需要新建或调整 Models、Data、Repositories、ViewModels、Views、Services 中的任何文件，或需要确认表结构、外键关联方式、注册性质文件白名单、是否允许改动已有接口时，必须触发本技能。
---

# 项目架构规范

## 一、分层规则

工程按职责严格分层，**依赖方向单向向下**：Views → ViewModels → Repositories → Data → Models。上层可以依赖下层，下层**禁止**反向引用上层。

| 层 | 目录 | 职责 | 禁止事项 |
| --- | --- | --- | --- |
| Models | `Models/` | 实体类，**一表一类**，与数据库表一一对应 | 禁止放查询逻辑、禁止放 UI 相关属性 |
| Data | `Data/` | `DbContext`、实体配置、EF Core 迁移文件 | 禁止放业务逻辑，禁止在 DbContext 之外写 `SaveChanges` 的封装魔法 |
| Repositories | `Repositories/` | 数据访问。**所有 LINQ 查询只允许出现在这一层** | 禁止返回 `IQueryable` 给上层；禁止上层绕过 Repository 直接访问 DbContext |
| ViewModels | `ViewModels/` | 展示状态与命令，使用 **CommunityToolkit.Mvvm** | 禁止写 LINQ 查询；禁止直接 `new DbContext()`；禁止引用 `System.Windows` 控件类型 |
| Views | `Views/` | XAML 界面，只做布局与数据绑定 | **禁止写业务逻辑**（事件处理器里不得出现业务判断、数据访问、计算） |
| Services | `Services/` | 预留层：跨 Repository 的编排、导入导出、校验、第三方能力 | 当前可留空目录 + 接口，禁止因为“还没用到”而删掉该层 |

### 各层落地要点

- **Models**：纯 POCO，只描述字段与导航属性。日期/时间/枚举的转换细节放在 `Data` 层的实体配置里。
- **Data**：`AppDbContext` 暴露 `DbSet<Student>`、`DbSet<Course>`、`DbSet<CourseSchedule>`、`DbSet<StudentCourse>`。唯一索引、外键、级联行为在此统一配置（可用 `OnModelCreating` 或 `IEntityTypeConfiguration<T>`）。
- **Repositories**：每个聚合一个接口 + 实现，例如 `IStudentRepository` / `StudentRepository`。对外只暴露 `Task<List<T>>`、`Task<T?>`、`Task<bool>` 这类具体结果。
  - 构造函数一律注入 `IDbContextFactory<AppDbContext>`，每个操作方法内部 `using var ctx = _factory.CreateDbContext();`；**禁止**把 DbContext 存为字段跨方法复用（详见 `wpf-mvvm-conventions.md`）。
- **ViewModels**：只依赖 Repository 接口（构造函数注入），不依赖具体实现，便于后续替换与测试。
- **Views**：只允许 `{Binding}` / `{x:Bind}`、样式、模板、以及把用户操作转成命令绑定（`Command="{Binding XxxCommand}"`）。
- **Services**：需要跨多表编排（例如“给学生排一门课并校验时间冲突”）时，新建 Service，而不是把逻辑塞进 ViewModel。

## 二、数据表结构（九表）

### 1. Students（学生表）

| 字段 | 说明 | 约束 |
| --- | --- | --- |
| `Id` | 主键 | 自增整型 |
| `StudentNo` | 学号 | **唯一**（唯一索引） |
| `Name` | 姓名 | 必填 |
| `Gender` | 性别 | 必填 |
| `ClassName` | 班级名称 | 必填 |
| `Phone` | 联系电话 | 可空 |
| `Note` | 备注 | 可空 |

### 2. Courses（课程表）

| 字段 | 说明 | 约束 |
| --- | --- | --- |
| `Id` | 主键 | 自增整型 |
| `CourseNo` | 课程编号 | **选填**，**唯一**（唯一索引；留空存 NULL，SQLite 允许多个 NULL） |
| `CourseName` | 课程名称 | 必填 |
| `Teacher` | 任课教师 | 必填 |
| `Credit` | 学分 | **选填**，可空数值（SQLite REAL，double?） |

- **`CourseNo` 必须建立 UNIQUE 索引**：编号已知时必须防重；编号选填，留空统一存 NULL（不查重、允许多条），**禁止**用空字符串代替 NULL（唯一索引只允许一个空字符串）。

### 3. CourseSchedules（排课表）

| 字段 | 说明 | 约束 |
| --- | --- | --- |
| `Id` | 主键 | 自增整型 |
| `CourseId` | 关联课程 | **外键 → Courses.Id** |
| `DayOfWeek` | 星期 | **取值 1–7**（1=周一 … 7=周日） |
| `StartPeriod` | 起始节次 | 必填，整数 |
| `EndPeriod` | 结束节次 | 必填，整数，且 **`EndPeriod >= StartPeriod`** |
| `StartWeek` / `EndWeek` | 周次起止 | 必填；**新代码一律用它做周判断** |
| `WeekParity` | 单双周 | 0 每周 / 1 单周 / 2 双周，非空、默认 0；以**学期教学周序号**为准（第 1、3、5… 周为单周） |
| `WeekRange` | 周次范围 | **已废弃**，仅为历史数据保留，禁止读它做时间计算 |
| `Location` | 上课地点 | 必填 |

- **周过滤唯一入口**：`Models/WeekParityTypes.IsActiveInWeek(startWeek, endWeek, weekParity, week)` —— 课表网格、课表列表、空闲统计三处必须共用，禁止各写一份谓词。周序号本身经 `ISemesterService` 换算。
- 调休补课的**补课记录不做奇偶过滤**（调整是日期锚定的）；调休日按 `FollowWeekday` 照搬课表时照常应用奇偶，该周不存在的时段自然为空。

### 4. StudentCourses（学生选课中间表，多对多）

| 字段 | 说明 | 约束 |
| --- | --- | --- |
| `Id` | 主键 | 自增整型 |
| `StudentId` | 关联学生 | **外键 → Students.Id** |
| `CourseId` | 关联课程 | **外键 → Courses.Id** |
| `Semester` | 学期 | 必填，如 `2024-2025-1` |

- **`StudentId + CourseId + Semester` 三元组必须建立 UNIQUE 索引**，防止同一学期重复选同一门课。
- 本表是 Students 与 Courses 的多对多中间表，禁止额外增加业务职责。

### 5. Attendances（考勤表）

| 字段 | 说明 | 约束 |
| --- | --- | --- |
| `Id` | 主键 | 自增整型 |
| `StudentId` | 关联学生 | **外键 → Students.Id，级联删除** |
| `Date` | 考勤日期 | `DateOnly`，SQLite 存为 TEXT |
| `CourseId` | 关联课程/场合 | **可空外键 → Courses.Id**；`null` 表示非上课场合（如班级活动） |
| `Status` | 考勤状态 | 整型枚举 `AttendanceStatus`：0 出勤 / 1 迟到 / 2 旷课 / 3 请假 |
| `Note` | 备注 | 可空 |
| `CreatedAt` | 记录创建时间 | `DateTime` |

- **本表不设数据库唯一约束**：同一课程同一天可能有多节，是否已记录由 `UpsertBatchAsync` 在应用层按 (StudentId, Date, CourseId) 判定。
- 删除学生时考勤记录随外键级联删除；删除课程时对应考勤的 `CourseId` 置空（SetNull）。

### 7. SemesterSettings（学期设置表）

| 字段 | 说明 | 约束 |
| --- | --- | --- |
| `Id` | 主键 | 自增整型 |
| `SemesterName` | 学期名 | **唯一**；取值与 `StudentCourses.Semester` 一致 |
| `StartDate` | 第 1 周周一 | **可空**（未配置时周次换算降级返回 null，调用方提示而不崩溃） |
| `WeekCount` | 总周数 | 整型，默认 20；合法范围 1–30 |

- 学期列表来源于 `StudentCourses.Semester` 去重（与空闲时间统计同源），本表只存"起始日 + 总周数"这类配置。
- 周次换算：`周 = (日期 − 起始日) / 7 + 1`；早于起始日或超出总周数一律返回 null。

### 8. PeriodTimes（作息时间表）

| 字段 | 说明 | 约束 |
| --- | --- | --- |
| `Id` | 主键 | 自增整型 |
| `PeriodNo` | 节次号 | **唯一**，1–12 |
| `StartTime` / `EndTime` | 本节起止时刻 | `TimeOnly`（SQLite 存 TEXT），精确到分钟 |

- 12 节作息由**迁移的 `HasData` 写入种子**（每日作息，非教学数据），设置页可整体修改。
- 消费方只读：课表页行头图例（含"午休/晚休"分隔，视图常量不落库）、空闲统计矩阵行标签（节次对 + 时间范围）。
- 服务 `IPeriodTimeService` 缓存并在保存后广播 `Changed`，各页面据此即时刷新行头。

### 9. ScheduleAdjustments（调休补课调整表）

| 字段 | 说明 | 约束 |
| --- | --- | --- |
| `Id` | 主键 | 自增整型 |
| `Semester` | 学期 | 必填，与 `StudentCourses.Semester` 一致 |
| `AdjustmentType` | 0 停课 / 1 补课 / 2 放假 / 3 调休上课日 | 常量化于 `ScheduleAdjustmentTypes`，禁止各处硬编码数字 |
| `CourseId` | 课程 Id | **可空**：停课/补课必填，放假/调休整天类型不填 |
| `AffectedDate` | 调整落在的日历日 | 必填（TEXT） |
| `StartPeriod` / `EndPeriod` | 节次起止 | **可空**：节次级类型必填 |
| `FollowWeekday` | 该日按周几上课 | **可空**，仅调休上课日使用（1–7） |
| `NewLocation` / `Note` | 补课教室 / 备注 | 可空 |
| `CreatedAt` | 创建时间 | 必填 |

- **「调课」不单独建类型**：由「停课 + 补课」两条记录组成，弹窗一次事务写入；**「放假」支持日期区间，保存时按天展开为多条**。
- 消费方：课表页网格逐列叠加（放假列置空、调休列按 FollowWeekday 取课、停课弱化标记、补课新增格）；`FreeTimeRepository` 的三个查询按同一套规则换算（放假→全班空闲、调休→换天匹配、停课→剔除、补课→加入）。
- 前置依赖：`SemesterSettings.StartDate` 未配置时**整模块禁用**且不做任何叠加；日期换算一律走 `ISemesterService`，禁止重复实现周计算。

## 三、硬性约束

1. **新功能 = 新增文件**：新增能力通过新建实体/迁移/Repository 方法扩展的新文件实现；**禁止修改已有公共接口**（已有接口的方法签名、已有实体字段、已有 ViewModel 命令名一律不动）。
   - 需要扩展行为时：新增接口方法放在**新接口**中（如 `IStudentQueryRepository`），或新增 Service 包装已有 Repository。
   - 如需破坏性变更，必须先向用户说明并获得确认，再单独提交一次 `refactor:`。
2. **所有外键用 Id 关联**：表与表之间只通过整型 Id 外键关联，**严禁使用姓名、课程名、班级名等文字字段做关联或连接查询条件**。
3. 查询必须落在 Repositories 层，ViewModel 拿到的是已经组装好的结果对象。
4. 任何改动都不得绕过 EF Core 直接操作数据库文件（见 `efcore-sqlite-migrations.md`）。

## 四、注册性质文件（新增实体/模块时唯一允许改动的既有文件）

新增功能**一律新增文件**，只允许在下列 5 个"注册性质"文件里做**纯新增**（这是硬性约束 1 的明确例外，无需再逐次请示）：

| 注册性质文件 | 允许的改动 |
| --- | --- |
| `Services/FeatureRegistry.cs` | 仅新增一条模块定义（Id/显示名/图标/默认可见）与对应类型参数 |
| `App.xaml.cs` 的 DI 注册段 | 仅新增注册行（`AddSingleton` 等）与 `FeatureRegistry.Register(...)` 的类型实参 |
| 页面模板注册位（`Views/MainWindow.xaml` 的 `Window.Resources` 资源段） | 仅新增一条隐式 `DataTemplate`（`DataType` = 模块 ViewModel 类型） |
| 弹窗模板注册位（`Views/Dialogs/DialogShell.xaml` 的 `Window.Resources` 资源段） | 仅新增一条隐式 `DataTemplate`（`DataType` = 弹窗 ViewModel 类型） |
| `Data/AppDbContext.cs` | **新增实体时**仅允许纯新增一条 `DbSet` 属性 + 对应实体配置段（索引、外键、级联行为） |

**冻结条款**：上述文件里既有的属性、既有的配置段、既有的注册行一律冻结；已提交的迁移一律冻结。新增实体必须配套一次 EF Core 迁移（见 `efcore-sqlite-migrations.md`）。

**纯新增条款**（与冻结条款配套，二者不矛盾）：

1. **冻结的是既有成员的签名与行为，不是文件字节不可写。** 在既有文件上纯新增成员（既有成员一字不动）**视同新增文件，可直接执行、无需请示**；完成后在汇报中列出「对既有文件新增的成员清单」。
2. 新增成员必须属于该文件的**固有职责**（如：弹窗服务新增弹窗方法 ✓；把无关功能硬塞进某个文件 ✗）。跨职责时停下请示。
3. **禁止为绕开文件修改做语法性绕路**：不得为了不动既有文件而把文件拆成 `partial`、不得用「换个名字另建一套同类设施」的方式与既有设施并存。
4. **指令与库现状冲突时的决策顺序**：① 既有设施已满足需求 → 直接复用；② 需要纯新增成员 → 按本条款执行；③ 需要改既有成员的签名或行为 → **停下请示**；④ 同名但职责不同 → **停下请示**；⑤ 指令中的实现细节（类型名 / 格式 / 参数形式 / 存储方式等）与库内既有惯例冲突 → **按库内惯例执行 + 列入汇报**（仅当指令明确标注"刻意偏离惯例"并说明理由时，才按指令执行）。
5. **库内惯例优先条款**：指令中的类型/机制/命名描述一律视为**意图占位**，判断基准以**库现状**为准；发现与既有惯例冲突时按惯例执行并在汇报中说明，不必为此单独请示。
6. **迁移联动条款**：迁移涉及**列的空性 / 类型变更**时，对应实体属性与镜像 DTO 属性的同步类型变更**属于迁移动作的组成部分**，视同迁移文件、免请示（完成后汇报清单）；类型变更引起的既有使用点**编译警告**在使用点做最小空值修复（空判断 / 空合并），**禁止** `#pragma` / `NoWarn` 压制；**禁止**用 `null!` 让非空属性承接数据库 NULL，也禁止 `partial` 拆分、影子属性等语法性绕路。


## 五、自检清单

- [ ] 新增文件是否放在了正确的层？
- [ ] 是否出现了 ViewModel / View 中的 LINQ 查询？（应下沉到 Repository）
- [ ] 是否有文字字段参与关联？
- [ ] 是否修改了已有公共接口？（应改为新增）
- [ ] 是否改动表结构却没用 Migration？
- [ ] 唯一索引是否齐全（`Students.StudentNo`、`Courses.CourseNo`、`StudentCourses(StudentId, CourseId, Semester)`）？
- [ ] 改动是否只落在注册性质文件里（FeatureRegistry / App.xaml.cs DI 段 / 页面模板注册位 / 弹窗模板注册位 / AppDbContext 的纯新增）？
- [ ] 对既有文件的新增成员是否属于该文件固有职责，且既有成员一字未动？
