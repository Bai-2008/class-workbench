---
name: efcore-sqlite-migrations
description: EF Core + SQLite 的数据库变更流程。当需要新增或修改实体类字段、表结构、索引、外键，或需要执行 dotnet ef migrations add / dotnet ef database update、排查迁移与数据库不一致、处理已有迁移文件时，必须触发本技能。
---

# EF Core + SQLite 迁移规范

## 一、唯一合法的改库流程

改动实体后，**必须**按以下顺序执行，两步缺一不可：

```powershell
# 1) 生成迁移（名称必须描述性、英文或拼音、kebab/Pascal 均可，禁止 add Migration1 这类无意义名称）
dotnet ef migrations add AddCourseScheduleTable

# 2) 应用到数据库文件
dotnet ef database update
```

- 迁移名称要能表达本次变更内容，例如：
  - `AddStudentNoteColumn`
  - `AddUniqueIndexOnStudentNo`
  - `CreateStudentCoursesRelation`
  - `AddCourseScheduleForeignKeys`
- 若 `dotnet ef` 命令不存在，先安装工具：`dotnet tool install --global dotnet-ef`（并确认版本与 EF Core 包匹配）。

## 二、硬性红线

1. **迁移文件一旦提交，不允许修改，只能新增迁移。**
   - 写错了、漏了字段：不要编辑旧迁移的 `Up()`，改为 `dotnet ef migrations add FixXxx` 新增一个迁移来修正。
   - 迁移已应用到数据库后更是如此：改旧迁移会导致模型快照与数据库状态不一致。
2. **禁止直接修改 `.db` 文件**，包括但不限于：用 DB Browser / SQLite CLI / 脚本执行 `ALTER TABLE`、`DROP TABLE`、手工改数据来让程序跑通。
3. 禁止手工编辑 `*ModelSnapshot.cs` 来“对齐”结构；快照由迁移生成器维护。
4. 禁止删除数据库文件“重来”来绕过迁移问题；如确需重建（仅限开发期且无数据价值），必须先向用户说明并获得确认。

## 三、标准操作步骤

1. 修改 `Models/` 下的实体，或在 `Data/` 下调整实体配置（索引、外键、必填、长度）。
2. 执行 `dotnet build` 确认 0 error（迁移生成依赖可编译的模型）。
3. 执行 `dotnet ef migrations add <描述性名称>`。
4. **审查生成的迁移文件**：确认 `Up()` 里的表/列/索引/外键与预期一致（特别注意唯一索引与 `DayOfWeek` 1–7 等约束）。
5. 执行 `dotnet ef database update`。
6. 再次 `dotnet build`，并启动程序验证核心链路（见 `build-and-verify.md`）。
7. 提交信息中包含迁移文件名，例如：`feat(data): 新增 CourseSchedules 表及外键约束`。

## 四、SQLite 注意事项

- SQLite 对“删列/改类型”支持有限，EF Core 会采用重建表的方式；生成迁移后务必人工检查是否出现意外的数据丢失操作（如 `DROP TABLE` 后重建且未搬运数据）。
- 修改列类型或名称时，优先“新增列 → 迁移数据 → 保留旧列一个版本”，避免破坏已有数据。
- 连接字符串指向的 `.db` 文件路径必须与应用运行时使用的路径一致，避免“迁移打到一个库、程序连另一个库”。
- 索引与外键约束（如 `StudentCourses(StudentId, CourseId, Semester)` 唯一索引）必须在实体配置中声明后再生成迁移，不要事后用 SQL 补。

## 五、自检清单

- [ ] 表结构变更是否通过新增迁移完成？
- [ ] 是否没有修改任何已提交的迁移文件？
- [ ] 是否没有直接动过 `.db` 文件？
- [ ] 迁移名是否具备描述性？
- [ ] 是否已 `dotnet ef database update` 并重启程序验证？
