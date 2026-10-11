---
name: wpf-mvvm-conventions
description: WPF + CommunityToolkit.Mvvm 的写法约定。当需要编写或修改任何 ViewModel（属性、命令、加载/刷新逻辑）、任何 XAML 视图与数据绑定、ObservableCollection 集合属性，或需要判断某段逻辑该写在 View 还是 ViewModel 时，必须触发本技能。
---

# WPF MVVM 写法约定

## 一、ViewModel 使用 CommunityToolkit.Mvvm

依赖包：`CommunityToolkit.Mvvm`。ViewModel 必须继承 `ObservableObject`（或使用 `[ObservableRecipient]` 等基类）。

### 1. 属性：使用 `[ObservableProperty]`

- 使用**源生成器**写法，手动写 `SetProperty` 视为不符合规范。
- 字段用 `_camelCase`，生成的属性名自动为 `PascalCase`。
- 需要联动刷新时，使用 `[NotifyPropertyChangedFor(nameof(Other))]`，不要手写事件订阅。

```csharp
public partial class StudentListViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private Student? _selectedStudent;

    [ObservableProperty]
    private ObservableCollection<CourseScheduleItem> _schedules = new();

    public bool HasSelection => SelectedStudent is not null;
}
```

### 2. 命令：使用 `[RelayCommand]`

- 方法名 `Xxx` → 生成命令 `XxxCommand`，方法内**只做编排**（调用 Repository、更新属性），不写 LINQ。
- 异步方法命名为 `XxxAsync` → 生成 `XxxCommand`（异步命令），自动支持 `IsRunning`。
- 需要禁用条件时，用 `[RelayCommand(CanExecute = nameof(CanXxx))]`。
- 需要主动重算可用性时调用 `XxxCommand.NotifyCanExecuteChanged()`。

```csharp
[RelayCommand]
private async Task LoadStudentsAsync()
{
    var students = await _studentRepository.GetAllAsync();
    Students = new ObservableCollection<Student>(students);
}

[RelayCommand(CanExecute = nameof(CanRefreshSchedule))]
private async Task RefreshScheduleAsync()
{
    var items = await _scheduleRepository.GetByStudentIdAsync(SelectedStudent!.Id);
    Schedules = new ObservableCollection<CourseScheduleItem>(items);
}

private bool CanRefreshSchedule() => SelectedStudent is not null;
```

- 生成器要求类必须是 `partial`，且不能手写与生成成员同名的属性/命令。

### 3. 依赖注入（方案已定死，不得改用其他写法）

依赖包：`Microsoft.Extensions.DependencyInjection`（DbContext 注册使用 EF Core 提供的 `AddDbContextFactory`）。

- **组合根**：在 `App.xaml.cs` 的 `OnStartup` 中构建 `ServiceProvider`，在 `OnExit` 中释放；全部注册集中在这一处。
- **DbContext 用工厂注册**：`services.AddDbContextFactory<AppDbContext>(...)`，注入类型为 `IDbContextFactory<AppDbContext>`；**禁止**用 `AddDbContext` 让 DbContext 长生命周期存活（会带来跟踪残留与并发异常）。
- **Repository 一律注入 `IDbContextFactory<AppDbContext>`**，每个操作方法内部创建并释放上下文：

  ```csharp
  public sealed class StudentRepository : IStudentRepository
  {
      private readonly IDbContextFactory<AppDbContext> _factory;

      public StudentRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

      public async Task<List<Student>> GetAllAsync()
      {
          using var ctx = _factory.CreateDbContext();   // 每个方法一个短生命周期上下文
          return await ctx.Students.AsNoTracking().OrderBy(s => s.StudentNo).ToListAsync();
      }
  }
  ```

- **禁止长生命周期 DbContext**：不得把 `AppDbContext` 存为 Repository 字段跨方法复用，不得注入到 ViewModel / Service，不得用 `static` 缓存。
- ViewModel / Service 只通过构造函数接收 Repository / Service **接口**；View 的 `DataContext` 由组合根用 `ServiceProvider.GetRequiredService<TViewModel>()` 解析后设置。
- 新增 Repository 时只在组合根追加一行注册——这是本规范明确的唯一例外：`App.xaml.cs` 是唯一允许随新增功能修改的已有文件。
- 禁止在 ViewModel 里直接 `new AppDbContext()` 或触发 `SaveChanges`。

```csharp
// App.xaml.cs（组合根）
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    var services = new ServiceCollection();
    services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(DatabasePathProvider.GetConnectionString()));
    services.AddSingleton<IStudentRepository, StudentRepository>();   // 新增 Repository 只在此追加一行
    services.AddTransient<StudentListViewModel>();

    _serviceProvider = services.BuildServiceProvider();
}

protected override void OnExit(ExitEventArgs e)
{
    _serviceProvider?.Dispose();
    base.OnExit(e);
}
```

## 二、View 与 ViewModel 只通过数据绑定通信

- 绑定方式：`DataContext` 设为对应 ViewModel；XAML 中一律 `{Binding Xxx}`（或 `{Binding Path=Xxx}`）。
- **禁止**在 `*.xaml.cs` 里写业务逻辑：
  - 事件处理器只允许做纯 UI 动作（如 `Focus()`、`Close()`、滚动定位），且必须能一眼看出不涉及任何业务判断。
  - 业务动作一律通过 `Command="{Binding XxxCommand}"` + `CommandParameter` 完成。
- **禁止**在 View 里直接访问 Repository、DbContext、Service。
- 值转换用 `IValueConverter`；复杂展示用 `DataTemplate`，不要用代码拼控件。
- 双向绑定（表单输入）需显式 `Mode=TwoWay`，并确认 ViewModel 属性带 `[ObservableProperty]`。

## 三、集合属性使用 ObservableCollection

- 所有会随数据变化的列表属性类型必须是 `ObservableCollection<T>`（禁止 `List<T>` 直接暴露给 View）。
- **刷新集合的正确做法**：重新赋值整个 `ObservableCollection`（属性变更通知足以让绑定刷新），例如 `Schedules = new ObservableCollection<T>(items);`
- 增量更新时使用 `Add` / `Remove` / `Clear`，禁止直接操作绑定后的 `ItemsSource`。
- 需要筛选/排序视图时使用 `CollectionViewSource` 或 `ICollectionView`，不要改动底层集合。
- 集合内元素类型建议使用**展示模型**（如 `CourseScheduleItem`），由 Repository 返回并已组装好。

## 四、导航与页面装载（ViewModel-first，已定稿）

导航采用 **ViewModel-first + 隐式 DataTemplate**，不得再引入其它装载方式。

- `MainViewModel.CurrentViewModel`（`object`）承载当前模块的页面 ViewModel；主窗口内容区固定为 `<ContentControl Content="{Binding CurrentViewModel}" />`。
- 各模块的 View ↔ ViewModel 靠**隐式 DataTemplate**（`DataType` = ViewModel 类型，内容 = 对应 View）匹配，View 的 `DataContext` 由模板继承，**构造函数零注入、后台零逻辑**。
- 模块的 ViewModel 类型由组合根在 `FeatureRegistry.Register(...)` 时注入；Services 层因此不引用 ViewModels / Views 层。
- **服务定位（从容器解析）只允许出现在 `MainViewModel` 的导航层**，按 `FeatureModule.ViewModelType` 解析并缓存实例；业务 ViewModel 与 View 一律禁止服务定位。
- 各模块 ViewModel 在组合根注册为 **`AddSingleton`**：模块切走再切回时，筛选、选中项等状态必须保留。

### 新模块接入清单（照此执行，不需要再请示）

新增一个功能模块，**只允许触碰「注册性质文件」**（完整清单见 `project-architecture.md` 第四节），其余一律新增文件：

1. `Services/FeatureRegistry.cs`：加一条模块定义（Id / 显示名 / 图标路径 / 默认可见）+ 对应类型参数。
2. `App.xaml.cs`：`FeatureRegistry.Register(...)` 传入新模块的 ViewModel 类型；DI 段加 `services.AddSingleton<XxxViewModel>();` 及其依赖注册。
3. 页面模板注册位（`Views/MainWindow.xaml` 的 `Window.Resources`）：加一条 `<DataTemplate DataType="{x:Type vm:XxxViewModel}"><views:XxxPage /></DataTemplate>`。
4. 若模块要新增实体/新表：`Data/AppDbContext.cs` 里纯新增一条 `DbSet` 属性 + 对应实体配置段，并配套一次 EF Core 迁移。
5. 若模块有弹窗：弹窗模板注册位（`Views/Dialogs/DialogShell.xaml` 的 `Window.Resources`）加一条 `<DataTemplate DataType="{x:Type vm:XxxEditViewModel}"><dialogs:XxxEditView /></DataTemplate>`。

### 模板放置规则（隐式 DataTemplate 的注册位 = 该模板消费者的宿主窗口资源段）

WPF 的资源查找沿元素树向上、最后到 `Application.Resources`，因此**模板必须放在实际渲染它的那个窗口的资源段里**：

| 模板种类 | 注册位 | 谁看得见 |
| --- | --- | --- |
| 页面模板（模块 ViewModel → 模块页面） | `MainWindow.xaml` 的 `Window.Resources` | 主窗口内容区 `ContentControl` |
| 弹窗模板（弹窗 ViewModel → 弹窗编辑视图） | `DialogShell.xaml` 的 `Window.Resources` | 弹窗壳的内容区 `ContentControl` |

- 两处都是**注册位，适用「纯新增条款」**：新增模板直接执行、无需请示，完成后汇报即可。
- **不要**把弹窗模板写进 `MainWindow.xaml`：主窗口的资源段不在弹窗的元素树上，弹窗内容会退化成"类型名文本"。
- **演进预案**：将来出现新的窗口类型也要消费 ViewModel 内容时，做专项上收（抽独立 `ResourceDictionary` + `App.xaml` merge）。那属架构演进、不是日常新增，届时停下申请。

### 弹窗基础设施（DialogShell + IDialogService.ShowDialog）

- 通用弹窗壳 `Views/Dialogs/DialogShell.xaml`，**壳只含三部分**：标题（绑 `Title`）、内容区（`ContentControl` 绑自身 `Content`，由隐式模板渲染）、确定/取消按钮区（绑 `ConfirmCommand` / `CancelCommand`，`CanExecute` 联动禁用态）。
- `Window` 设 `SizeToContent="WidthAndHeight"` + `ResizeMode="NoResize"`，尺寸由各编辑视图内容自适应撑开，**不写死宽高**。
- 弹窗 ViewModel 继承 `ViewModels/DialogViewModelBase`，暴露 `Title` / `DialogResult` / `ConfirmCommand` / `CancelCommand`，**不与壳耦合**（壳不认识任何具体弹窗类型）。
- 打开方式：`IDialogService.ShowDialog<TVm>(TVm vm)`（既有成员冻结、此方法为纯新增）；壳由服务创建，`Owner` 用 `DialogService.GetOwner()`（活动窗口优先，回退 `Application.Current.MainWindow`）；`DialogResult` 由 ViewModel 设置，服务监听并关闭窗口。
- 弹窗只负责**收集与校验输入**（`CanExecute` 不满足时按钮禁用并给提示）；实际持久化由发起它的页面 ViewModel 调 Repository 完成，保存后即时刷新列表与汇总。
- **勾选类控件（`RadioButton` / `CheckBox`）禁止直接 TwoWay 绑定布尔属性**：WPF 点击时走 `SetCurrentValue`，只改视觉值、不回写绑定源（界面看着已切换、ViewModel 仍是旧值，且极难排查）。统一写法：`IsChecked="{Binding Xxx, Mode=OneWay}"` 只做单向显示 + `Command="{Binding ToggleXxxCommand}"` 由 ViewModel 改状态；勾选项变化需要通知父 ViewModel 时，用构造回调（如 `StudentSelectionItem(..., onSelectionChanged: RefreshValidation)`），不要靠 View 后台代码。

### 冻结条款

- **TemplateSelector 模式已废弃，禁止复活**（`ModuleTemplateSelector` 已删除），也不要再用 `DataContext="{Binding DataContext, RelativeSource=...}"` 之类跨层注入 DataContext。
- **`MainViewModel` 与 `MainWindow` 的结构冻结**：改动需专项申请；新增模块不得修改它们（除上面清单允许的注册行/模板行）。

## 五、命名与文件组织

- 每个 ViewModel 与 View 一一对应：`Views/StudentListView.xaml` ↔ `ViewModels/StudentListViewModel.cs`。
- ViewModel 类名以 `ViewModel` 结尾，命令以 `Command` 结尾，异步方法以 `Async` 结尾。
- 每个 ViewModel 只负责一个视图的数据与交互，跨视图共享逻辑放到 Services。

## 六、自检清单

- [ ] 属性是否用 `[ObservableProperty]`，命令是否用 `[RelayCommand]`？
- [ ] 类与生成器约束是否满足（`partial`、无重名成员）？
- [ ] 是否所有业务动作都走了 Command，XAML 后台代码无业务逻辑？
- [ ] 集合属性是否为 `ObservableCollection<T>`？
- [ ] ViewModel 是否只依赖接口、且不含 LINQ 查询？
- [ ] Repository 是否注入 `IDbContextFactory<AppDbContext>` 并在每个方法内 `using var ctx = ...`，没有把 DbContext 存为字段？
- [ ] 新增 Repository 时是否只在 `App.xaml.cs` 组合根追加了注册行，没有改动其他已有文件？
- [ ] 新增模块是否只触碰「注册性质文件」（FeatureRegistry / App.xaml.cs DI 段 / 页面模板注册位 / 弹窗模板注册位 / 新增实体时的 AppDbContext 纯新增）？模块 ViewModel 是否注册为 Singleton？
- [ ] 对既有文件是否只做了纯新增（既有成员一字未动、且属该文件固有职责）？
- [ ] 弹窗模板是否放在 `DialogShell.xaml` 资源段（而非 `MainWindow.xaml`）？
- [ ] 页面 View 的 `DataContext` 是否靠隐式模板继承，构造函数零注入、后台零逻辑？
- [ ] 勾选类控件（Radio/CheckBox）是否用 `Mode=OneWay` + `Command`，没有直接 TwoWay 绑定？
- [ ] 是否没有引入 TemplateSelector 或跨层 DataContext 注入？

## 验收对齐条款

需求方给的验收标准里常用「列 / 格 / 卡片 / 表头」这类**界面指称**，而库内实际结构未必对得上（例如「学生课表页」实际是**按课程列出的列表**，没有"日期列"和"时段格"）。处理规则：

1. **指称以库内实际结构为落点**：先确认该界面元素在库里是否存在，再决定验收怎么写。
2. **不符或冲突时停下对齐（决策顺序 ③）**：验收指称与实际结构不符，或"冻结范围"与"验收意图"互相矛盾时，**停下请示**——既不许可为凑字面验收擅自改结构，也不许可擅自改写验收去迁就结构。
3. **对齐方案必须给两案**：同时给出「**满足验收意图**（可能需新增元素/局部重构）」与「**改动最小**（只追加、不改既有展示）」两个方案及各自代价，由需求方裁定后再动手。
4. **裁定结果写入代码注释或提交信息**，避免下一次再为同一处结构产生同样的歧义。

> 实例：某轮需求写「课表页各列标放假、周六列显示周二课程」，而该页是课程列表。对齐后裁定为「新增独立的周视图网格 UserControl，默认展示网格、列表原样保留」，随后所有叠加渲染都落在网格上。

## 重构不变性条款

凡验收要求「行为不变 / 渲染不变 / 输出一致」的**重构**，验证方式一律是**新旧快照机器比对零差异**，不靠目测、不靠人工抽查：

1. **快照打在计算结果层**，不打在像素层：取「计算结果集合 + 降级标记 / 错误标记」这类渲染输入。渲染输入不变，渲染即不变；像素级比对不作为要求。
2. **新旧并存期生成旧快照**：切换前让旧逻辑仍在工作区，把旧口径的结果序列化成快照（JSON 等）落盘。
3. **不采样，全量铺开**：能逐日就逐日（学期百天量级全跑），能逐人/逐课就逐人逐课；两侧至少各取两个样本（例如网格侧一名有调休或双周课的学生 + 一名普通课表学生，空闲侧一名多课学生 + 一个班级）。
4. **禁止空证**：库内缺少触发条件（无调整记录、无双周课）时，先构造最小测试数据（一段放假、一条补课、一条调休、一门双周课）再快照；空库上的零差异不构成证明。
5. **旧实现不进提交**：比对用的旧代码只存在于临时验证程序，用完删除，不留在仓库。
6. **零差异才提交**；一旦发现新旧无法同时归零（说明两处旧实现彼此已有分歧）→ **停下报告差异点，请需求方裁定哪边是正确语义**。不得自行择一，也不得借重构顺手修正——语义修复属独立提交。
7. **顺序**：重构先行、消费方后接。重构提交只做等价性搬运，功能接入（如新增页面的取数口径）放后续提交，避免把「服务是否正确」与「功能是否好用」混进同一提交。
8. **多提交任务**默认顺序执行完、末尾统一汇报；允许开工前先列执行顺序，但列完即开工，方案送审不阻塞执行。
## 身份判定条款

- **去重键 / 实体同一性判定是领域断言，不是技术细节**：键选错会把不同实体粘成一个，错误随导入扩散后极难拆解。凡以「名称」或「名称组合」做身份匹配的，**首次真实数据导入后必须人工抽查合并误差**，导入报告必须含「名师相同未合并」「时段部分重叠」明细。
- 课表导入的**课程同一性** = 课程名 + 教师 + **时段集合完全一致**（星期/起止节次/起止周/单双周逐项相等，集合比较、与排列顺序无关）；判定在解析归一化后的结构化字段上进行，不在原文文本上。
  - 完全一致 → 复用同一门课，只建立/更新学生关联；
  - 名师相同、时段集合不同 → 属于**不同开课班**，按导入时段新建课程；有交集时报告附「时段部分重叠，请人工核对」；
  - **绝不增删既有课程的时段**；判定与导入先后顺序无关。
## 破坏性操作分级条款

破坏性操作按影响范围分两级，确认强度递增：

- **一般删除（单实体 / 单学生操作：删课程、清空某生课表）**：确认框必须**列明影响数量**——关联学生 N 人、考勤记录 M 条（保留）、涉及该课程的调整记录 K 条；执行后向前看的列表/统计即时消失，历史记录照常显示。
- **大规模清空（全局 / 批量：清空所有人课表、清空学期）**：**键入确认词**（如「清空」）方可执行 + **执行前自动调用 BackupService** + 完成后在提示中给出备份路径；备份异常不阻塞操作，但要如实报出失败详情。
- 两条通用要求：
  1. **边界收窄**：自动清扫（如"零关联课程自动删除"）只作用于触发它的那次操作范围（例如本次导入覆盖所解除的关联），**不得扩到全库**——手工创建、从未参与导入的实体任何流程都不得清除；
  2. **历史快照**：历史记录（考勤等）若只存外键，删除主实体后会出现"看不到名称"的残缺历史；应在记录上落**名称快照**字段并按当前值回填存量，历史展示一律读快照字段。
## 依赖序显式条款

- 任务顺序中凡含**语义依赖**（快照先于删除、迁移先于依赖该结构的操作、仓储先于消费它的命令），必须**显式标注「依赖序」**，且**不作为偏好开放调整**。
- 执行者向作者呈现可调整项时，**依赖步骤必须声明不可移动**；提出任何顺序调整建议前，**必须先做依赖检查**。
- 实例：考勤课程名快照（实体+迁移+回填）必须先于「删除课程 / 清空全部课表」——否则课程实体一删，历史考勤的课程名再无来源可回填（外键置空后永久悬空）。

## 验收粒度条款

- 验收写「仍在 / 不变 / 正确」时**必须定义粒度**，可选粒度：**实体存在 → 字段一致 → 逐字段相等**（必要时再加"数量一致"）。
- **一词多义的验收等于没验**。实例：原「新增裸课程执行导入后仍在」在实测中暴露多义（课程在但时段为 0 也算"仍在"），后升级为「实体存在 + 时段数与创建时一致 + 时段逐字段一致（星期/起止节次/起止周/单双周）」。
## 零分叉请示条款

- **请示的合法前提是存在真实分叉**：两选项都合规、既有约定未覆盖该情形、或既有约定之间互相冲突。**被约定明确禁止的选项不构成选项**——此时只有一个合规解，直接执行。
- **发问前置检查**：自己的推荐、默认行为、既有约定三者是否同向？同向即**直接执行**，汇报用陈述句写「按 X 执行，依据 Y」，不发问。
- **合规执行天然免责**：可追责的只有违反约定；约定明确时，「发问 + 给默认值」的责任保险价值为零。
- **禁止「设默认值 + 仍发问」**：默认值已经暴露你已知答案，此时发问等于不肯为已知答案署名。
- 实例：本项目已约定「弹窗一律走 DialogShell」，则 WPF 原生 `MessageBox` 不构成候选，不存在二选一的分叉；多写一个弹窗 View 属**合规成本**，不是权衡项。
## 探针前后取样条款

**验证性探针必须在操作前后双取样**：单次事后取样无法区分「从未存在」与「被清除」，**未取样到不构成状态被改变的证据**。
实例：某次探针只在导入后查时段数、得 0，据此怀疑"导入清掉了手工课程的时段"；补上"创建后立刻取样"这一刀后，实测创建后即为 2、导入后仍为 2 且逐字段一致，结论是**探针写法问题**而非产品缺陷。凡断言"某操作破坏了 X"，必须先给出 X 在该操作**之前**的取样值。
## 后到并入不重排主块

- 在途任务的**后到收项**（收尾、沉淀、并入、规范），处理时机 = **主块同轮余量**或**主块落地之后**；余量不足时，等待的是**小项**，不是主块。
- 后到输入**不因「最新」获得优先权**；执行顺序按**依赖序与主次**，不按到达序。
- 凡一轮的完整产出**仅为收尾项**，视同**主块顺延**，必须在汇报中**显式标注**，不得以"已完成"含糊呈现。
## 第七族条款：标注即许可（反模式）

- **显式标注顺延是记录义务，不是授权路径**。标注后必须附**原因**（余量实测 / 依赖障碍）；**无原因的顺延视同回避**。
- **同一主块连续顺延 ≥ 2 次后，禁用「整块不可分割」作理由**：主块必须按依赖序**切分上桌**（每刀仍需满足：依赖序正确 / HEAD 可编译可运行 / 有该刀自己的验收 / 无半成品）。
- **流程条款是纪律的载体**：发现条款可被用来把违规"合规化"时，**须上报条款偏离，不得利用偏离**。
- 本条款与「后到并入不重排主块」**均不得作为顺延依据被引用**。

## 写入判据条款（文档类改动的成功标准）

- 文档/skill 类写入的**成功标准 = 版本控制显示改动**（`git status` 出现 modified，或产生了新 commit）。**脚本回显"已写入"不构成证据**。
- 两件仪器冲突（文件内容 vs 写入回显）时，**以版本控制为准**。
- 实例：某轮条款写入脚本回显成功、`git status` 却干净；随后只读审计确认文件里并无该条款 → 回显无效，须重写并以 git 判据验收。

## 后到并入不重排主块（追加）

- **同轮余量 = 主块提交落地之后的同轮时间**；收尾项**不得先于主块执行**。
