# AGENTS.md — 班级学生课表管理应用

> 本文件是本项目的最高优先级协作规范。任何 AI 代理（Agent）或开发者在本仓库中工作时，都必须先阅读并遵守本文件。

## 项目概况

- **项目名称**：班级学生课表管理应用（Students Database）
- **技术栈**：WPF（.NET 8） + SQLite + EF Core + MVVM
- **核心业务**：维护学生、课程、排课（课表）、学生选课关系、考勤记录与班费流水，支持按学生查看个人课表、统计全班空闲时段与考勤情况、班级班费记账。

## 一、语言规范

- **全程使用中文回复**：与用户的所有交流、代码注释、提交信息正文、文档说明，一律使用中文。
- 代码标识符（类名、方法名、属性名）遵循 C# 命名规范使用英文，不强制中文化。

## 二、构建与完成标准

- **每完成一个逻辑单元必须执行 `dotnet build`**，只有 **0 error** 才算完成该逻辑单元。
- 逻辑单元指：一个实体/一次迁移、一个 Repository 方法、一个 ViewModel 命令、一个 View 的数据绑定等可独立验证的最小改动集合。
- 构建失败时：**先完整读报错信息再动手改，禁止盲目重试**。必须定位到具体文件、行号与错误码（如 CS0246、CS1061），确认根因后再修改；不得反复执行同一命令碰运气。
- **禁止掩盖警告**：不得通过 `#pragma warning disable`、降低 `<WarningLevel>`、在 csproj 中配置 `<NoWarn>`（及任何等效手段）来消除警告。确属无法消除的警告，必须在对应代码处加注释说明原因，并向用户说明该警告的成因与为何无法消除。

## 三、数据库规范

- **数据库结构变更只允许通过 EF Core Migration**：
  - `dotnet ef migrations add <描述性名称>`
  - `dotnet ef database update`
- **禁止直接修改 `.db` 文件**：不得使用 DB Browser、SQLite 命令行或任何手工方式改动表结构/数据以绕过迁移。
- 迁移文件一旦提交，不允许修改，只能新增迁移（详见 `.dsh/skills/efcore-sqlite-migrations.md`）。

### 数据模型（九表）

| 表 | 关键字段 | 唯一性约束 |
| --- | --- | --- |
| `Students` | Id、StudentNo、Name、Gender、ClassName、Phone、Note | `StudentNo` **UNIQUE** |
| `Courses` | Id、CourseNo（**选填**）、CourseName、Teacher、Credit（**选填**） | `CourseNo` **UNIQUE**（留空存 NULL，允许多个 NULL） |
| `CourseSchedules` | Id、CourseId（外键）、DayOfWeek（1–7）、StartPeriod、EndPeriod、StartWeek、EndWeek、WeekParity（0每周/1单周/2双周，默认 0）、WeekRange（已废弃）、Location | 无唯一约束；`EndPeriod >= StartPeriod`；周过滤唯一入口 `WeekParityTypes.IsActiveInWeek` |
| `StudentCourses` | Id、StudentId（外键）、CourseId（外键）、Semester | `(StudentId, CourseId, Semester)` **UNIQUE** |
| `Attendances` | Id、StudentId（外键，级联删除）、Date、CourseId（可空外键，null=班级活动）、Status（0出勤/1迟到/2旷课/3请假）、Note、CreatedAt | 无唯一约束；是否已记录由 `UpsertBatchAsync` 在应用层按 (StudentId, Date, CourseId) 判定 |
| `Expenses` | Id、Date、Type（0收入/1支出）、Amount（decimal，**>0** CHECK）、Category、Description（必填）、Handler（经办人）、Note（可空）、CreatedAt | 无唯一约束；**余额不落库**，实时计算 = 总收入 − 总支出 |
| `SemesterSettings` | Id、SemesterName、StartDate（可空，第 1 周周一）、WeekCount（默认 20） | `SemesterName` **UNIQUE**；起始日未配置时周次换算降级返回 null |
| `PeriodTimes` | Id、PeriodNo（1–12）、StartTime、EndTime | `PeriodNo` **UNIQUE**；12 节作息以迁移 `HasData` 写入种子，设置页可改；课表/空闲统计行头只读它 |
| `ScheduleAdjustments` | Id、Semester、AdjustmentType（0停课/1补课/2放假/3调休上课日）、CourseId（可空）、AffectedDate、StartPeriod（可空）、EndPeriod（可空）、FollowWeekday（可空）、NewLocation（可空）、Note（可空）、CreatedAt | 无唯一约束；「调课」= 停课 + 补课两条（一次事务），「放假」按天展开；`(Semester, AffectedDate)` 索引 |

- 外键全部使用整型 Id 关联，**禁止用姓名、课程名等文字字段关联**。
- 字段级完整说明以 `.dsh/skills/project-architecture.md` 为准。

### 注册性质文件（新增实体/模块时唯一允许改动的既有文件）

新增功能一律新增文件；只允许在下列 5 个文件里**纯新增**（硬性约束 1 的明确例外，不需要逐次请示）：

1. `Services/FeatureRegistry.cs` —— 仅新增模块注册；
2. `App.xaml.cs` 的 DI 注册段 —— 仅新增注册行与 `FeatureRegistry.Register(...)` 实参；
3. 页面模板注册位（`Views/MainWindow.xaml` 的 `Window.Resources`）—— 仅新增一条隐式 `DataTemplate`；
4. 弹窗模板注册位（`Views/Dialogs/DialogShell.xaml` 的 `Window.Resources`）—— 仅新增一条隐式 `DataTemplate`；
5. `Data/AppDbContext.cs` —— **新增实体时**仅允许纯新增一条 `DbSet` 属性 + 对应实体配置段。

**冻结**：这些文件里既有的属性/配置段/注册行一律冻结；已提交迁移一律冻结。新增实体必须配套一次 EF Core 迁移。

**纯新增条款**（与冻结条款配套）：冻结的是既有成员的签名与行为，不是文件字节不可写——在既有文件上纯新增成员（既有成员一字不动、且新增成员属该文件固有职责）**视同新增文件、可直接执行、无需请示**，完成后在汇报中列出新增成员清单；需要改动既有成员签名/行为，或遇到"同名但职责不同"时，必须停下请示。禁止为绕开文件修改做 `partial` 拆分或"换名另建一套同类设施"的语法性绕路。

**决策顺序**：① 既有设施已满足 → 复用；② 需要纯新增成员 → 按纯新增条款执行；③ 需要改既有成员签名/行为 → 停下请示；④ 同名但职责不同 → 停下请示；⑤ 指令中的实现细节（类型名/格式/参数形式/存储方式）与库内既有惯例冲突 → 按库内惯例执行并列入汇报（仅当指令明确标注"刻意偏离惯例"并说明理由时才按指令执行）。判断基准一律以**库现状**为准：指令中的类型与机制描述视为**意图占位**。

**迁移联动条款**：迁移涉及列的空性/类型变更时，对应实体属性与镜像 DTO 属性的同步类型变更**属于迁移动作的组成部分**（免请示，完成后汇报清单）；类型变更引起的既有使用点编译警告在使用点做最小空值修复（空判断/空合并，**禁止** pragma/NoWarn 压制）；**禁止**用 `null!` 让非空属性承接数据库 NULL，也禁止 partial 拆分、影子属性等语法性绕路。

## 四、提交规范

- **commit 信息遵循 Conventional Commits**，前缀必须使用以下类型之一：
  - `feat:` 新增功能
  - `fix:` 缺陷修复
  - `refactor:` 重构（不改变外部行为）
  - 其他可用类型：`docs:`、`chore:`、`test:`、`style:`、`perf:`
- 格式：`<type>(<scope>): <中文简述>`，例如：
  - `feat(student): 新增学生列表加载与课表联动`
  - `fix(schedule): 修复周次范围解析越界`
  - `refactor(repository): 将 LINQ 查询从 ViewModel 下沉到 Repository`
- 一个提交只做一件事，禁止把无关改动混在同一次提交中。

## 五、依赖注入方案（已定死，不得改用其他写法）

- 引入 `Microsoft.Extensions.DependencyInjection`；在 **`App.xaml.cs` 的 `OnStartup`** 中构建 `ServiceProvider`（组合根），在 `OnExit` 中释放。
- 用 **`AddDbContextFactory<AppDbContext>`** 注册 `IDbContextFactory<AppDbContext>`；**禁止**用 `AddDbContext` 让 DbContext 长生命周期存活。
- 在组合根注册**全部 Repository 接口**；此后每新增一个 Repository，只在组合根追加一行注册。
  - 可改动的既有文件只有「三、数据库规范 · 注册性质文件」里列出的注册位（`FeatureRegistry.cs` / `App.xaml.cs` 的 DI 注册段 / 页面模板注册位 / 弹窗模板注册位 / `AppDbContext.cs` 的纯新增 DbSet 与配置段），以及第三节「纯新增条款」允许的既有文件纯新增成员；其余已有公共接口一律不动，新功能一律新增文件。
- **Repository 一律注入 `IDbContextFactory<AppDbContext>`**，每个操作方法内部 `using var ctx = _factory.CreateDbContext();`，用完即释放；**禁止**把 DbContext 存为 Repository 字段跨方法复用。
- ViewModel / Service 只通过构造函数接收 Repository / Service 接口，不接触 DbContext；View 的 `DataContext` 由组合根从 `ServiceProvider` 解析后设置。
- 细节与示例代码见 `.dsh/skills/wpf-mvvm-conventions.md`。

## 六、技能文件（Skills）

本项目的详细工程规范拆分在以下 4 个技能文件中，改动对应领域前必须先读取对应文件：

| 技能文件 | 作用 |
| --- | --- |
| `.dsh/skills/project-architecture.md` | 分层规则、九表结构、注册性质文件、纯新增/决策顺序/迁移联动条款、硬性约束 |
| `.dsh/skills/wpf-mvvm-conventions.md` | ViewModel/View 的 MVVM 写法约定 |
| `.dsh/skills/efcore-sqlite-migrations.md` | 实体变更 → 迁移 → 更新数据库的流程 |
| `.dsh/skills/build-and-verify.md` | 提交前的构建与核心链路自测流程 |
