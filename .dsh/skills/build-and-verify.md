---
name: build-and-verify
description: 提交前的构建与验证流程。当完成任何一个逻辑单元（实体、迁移、Repository、ViewModel、View 改动）准备继续下一步或提交代码、需要执行 dotnet build、需要做核心链路自测（学生列表加载、点击学生课表刷新）、或构建报错需要定位根因时，必须触发本技能。
---

# 构建与验证流程

## 一、必须先跑构建

```powershell
dotnet build
```

**通过标准：0 error、0 warning。** 只要出现 error 或 warning 就视为该逻辑单元未完成：

- error：必须修到 0 才能继续。
- warning：同样要处理（未使用变量、可空引用警告、异步方法缺 `await` 等），不允许“先留着以后再说”。
- **禁止掩盖警告**：不得用 `#pragma warning disable`、降低 `<WarningLevel>`、csproj `<NoWarn>`（及任何等效手段）让警告消失。确属无法消除的警告，必须在对应代码处加注释说明原因，并向用户说明成因与为何无法消除。

## 二、构建失败时的强制流程

1. **先完整读报错信息**：定位到具体文件、行号、错误码（如 `CS0246`、`CS1061`、`MSB3027`）。
2. 判断根因分类：
   - 编译错误：缺 using、类型不匹配、接口未实现、生成器约束未满足（如类未标 `partial`）。
   - NuGet/还原错误：包缺失、目标框架不匹配（本项目为 `net8.0-windows`）。
   - 文件占用：程序或 `dotnet ef` 仍在运行导致 `*.dll` / `*.db` 被锁定。
3. 只针对根因做**一次针对性修改**，再重新构建。
4. **禁止盲目重试**：不得在未阅读报错的情况下反复执行 `dotnet build`，不得靠改无关代码“试一试”。
5. 若同一错误连续两次修复无效，停止改动，把完整报错和已尝试的方案汇报给用户。

## 三、核心链路自测（提交前必做）

构建通过后，必须启动程序，手动跑通以下两条核心链路：

1. **学生列表加载**
   - 打开应用 → 学生列表正常显示。
   - 校验：学号、姓名、性别、班级等字段显示正确；空库时页面不崩溃、有合理空状态。
2. **点击学生 → 课表刷新**
   - 在列表中选中/点击某个学生 → 右侧（或下方）课表区域刷新为该学生的课程。
   - 校验：切换学生时课表内容随之变化（不是残留上一个学生的数据）；周次、星期、节次、地点、课程名、教师显示正确。
   - 快速连续切换多个学生，确认无异常、无数据错位。

## 四、回归范围

- 改动 Repository / 实体 / 迁移后：必须重跑上述两条链路。
- 改动 ViewModel / View 后：至少重跑受影响的视图，并确认其余页面仍能正常加载。
- 涉及表结构变更时：在 `dotnet ef database update` 之后**重启程序**再自测。

## 五、提交前检查清单

- [ ] `dotnet build` 结果为 0 error、0 warning。
- [ ] 未使用 `#pragma warning disable` / `NoWarn` / 降低 `WarningLevel` 等手段掩盖警告。
- [ ] 学生列表加载链路自测通过。
- [ ] 点击学生课表刷新链路自测通过（含快速切换）。
- [ ] 表结构变更已通过 Migration 落地并 `database update`。
- [ ] commit 信息符合 Conventional Commits（`feat:` / `fix:` / `refactor:` 等前缀）。
- [ ] 未修改任何已有公共接口（新功能通过新增文件实现）。

## 六、可直接使用的命令序列

```powershell
dotnet build                      # 必须 0 error 0 warning
dotnet ef migrations add <名称>    # 仅在有实体/结构变更时
dotnet ef database update         # 仅在有迁移时
dotnet run                        # 启动后手工跑核心链路自测
git status                        # 确认改动范围符合预期
git commit -m "feat(scope): 中文简述"
```
