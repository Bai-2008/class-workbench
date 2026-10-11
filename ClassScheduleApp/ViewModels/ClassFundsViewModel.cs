using System.Collections.ObjectModel;
using System.Globalization;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using ClassScheduleApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 班费记账页 ViewModel：4 张汇总卡 + 过滤栏 + 流水列表 + 记一笔/编辑/删除/导出。
/// 过滤条件变化、任何增删改保存后都会重算汇总并刷新列表；不引用其它 ViewModel。
/// </summary>
public partial class ClassFundsViewModel : ObservableObject
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IDialogService _dialogService;
    private readonly IExpenseExportService _exportService;
    private readonly IBackupService _backupService;

    private IReadOnlyList<string> _editableCategories = new List<string>();
    private string? _lastHandler;
    private bool _suppressReload;

    public ClassFundsViewModel(
        IExpenseRepository expenseRepository,
        IDialogService dialogService,
        IExpenseExportService exportService,
        IBackupService backupService)
    {
        _expenseRepository = expenseRepository;
        _dialogService = dialogService;
        _exportService = exportService;
        _backupService = backupService;

        // 备份恢复 → 数据库已被整体替换，流水与分类重新查询
        _backupService.Restored += (_, _) =>
        {
            _ = ReloadAsync();
            _ = ReloadCategoriesAsync();
        };

        var today = DateTime.Today;
        _suppressReload = true;
        try
        {
            FilterStart = new DateTime(today.Year, today.Month, 1);
            FilterEnd = today;
            TypeOptions = new ObservableCollection<ExpenseOption>(new List<ExpenseOption>
            {
                new("全部类型"),
                new("收入", ExpenseType.Income),
                new("支出", ExpenseType.Expense),
            });
            SelectedTypeOption = TypeOptions[0];
            CategoryOptions = new ObservableCollection<ExpenseOption>(new List<ExpenseOption> { new("全部类别") });
            SelectedCategoryOption = CategoryOptions[0];
        }
        finally
        {
            _suppressReload = false;
        }

        try
        {
            Task.Run(InitializeAsync).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            StatusMessage = $"初始化失败：{ex.Message}";
        }
    }

    // ── 过滤条件 ────────────────────────────────────────────────

    /// <summary>起始日期。</summary>
    [ObservableProperty]
    private DateTime? _filterStart;

    /// <summary>结束日期。</summary>
    [ObservableProperty]
    private DateTime? _filterEnd;

    /// <summary>类型下拉（全部/收入/支出）。</summary>
    [ObservableProperty]
    private ObservableCollection<ExpenseOption> _typeOptions = new();

    /// <summary>当前类型过滤。</summary>
    [ObservableProperty]
    private ExpenseOption? _selectedTypeOption;

    /// <summary>类别下拉（全部类别 + 预设 + 历史自定义）。</summary>
    [ObservableProperty]
    private ObservableCollection<ExpenseOption> _categoryOptions = new();

    /// <summary>当前类别过滤。</summary>
    [ObservableProperty]
    private ExpenseOption? _selectedCategoryOption;

    /// <summary>关键词（匹配说明/类别/经办人）。</summary>
    [ObservableProperty]
    private string _keyword = string.Empty;

    // ── 汇总卡（随过滤联动）─────────────────────────────────────

    /// <summary>当前余额。</summary>
    [ObservableProperty]
    private string _balanceText = "0.00";

    /// <summary>总收入。</summary>
    [ObservableProperty]
    private string _totalIncomeText = "0.00";

    /// <summary>总支出。</summary>
    [ObservableProperty]
    private string _totalExpenseText = "0.00";

    /// <summary>笔数。</summary>
    [ObservableProperty]
    private string _entryCountText = "0";

    // ── 列表与状态 ──────────────────────────────────────────────

    /// <summary>流水集合。</summary>
    [ObservableProperty]
    private ObservableCollection<ExpenseRecordInfo> _entries = new();

    /// <summary>当前选中行（双击编辑用）。</summary>
    [ObservableProperty]
    private ExpenseRecordInfo? _selectedEntry;

    /// <summary>操作提示。</summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>记一笔：打开通用弹窗壳收集输入，确定后落库。</summary>
    [RelayCommand]
    private async Task AddEntryAsync()
    {
        var viewModel = new ExpenseEditViewModel();
        viewModel.Initialize(null, _editableCategories, _lastHandler);

        if (_dialogService.ShowDialog(viewModel) != true || viewModel.Result is null)
        {
            return;
        }

        var result = await _expenseRepository.AddAsync(viewModel.Result);
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "保存失败。";
            return;
        }

        await ReloadAsync();
        StatusMessage = $"已记一笔：{viewModel.Result.Date:yyyy-MM-dd} {viewModel.Result.Category} {viewModel.Result.Amount:N2}。";
    }

    /// <summary>编辑：双击行或点行尾"编辑"按钮；不传参时用当前选中行。</summary>
    [RelayCommand]
    private async Task EditEntryAsync(ExpenseRecordInfo? entry)
    {
        var target = entry ?? SelectedEntry;
        if (target is null)
        {
            StatusMessage = "请先选中一行流水。";
            return;
        }

        var viewModel = new ExpenseEditViewModel();
        viewModel.Initialize(target, _editableCategories, _lastHandler);

        if (_dialogService.ShowDialog(viewModel) != true || viewModel.Result is null)
        {
            return;
        }

        var result = await _expenseRepository.UpdateAsync(target.Id, viewModel.Result);
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "保存失败。";
            return;
        }

        await ReloadAsync();
        StatusMessage = $"已修改流水：{viewModel.Result.Date:yyyy-MM-dd} {viewModel.Result.Category} {viewModel.Result.Amount:N2}。";
    }

    /// <summary>删除：先走确认框。</summary>
    [RelayCommand]
    private async Task DeleteEntryAsync(ExpenseRecordInfo? entry)
    {
        var target = entry ?? SelectedEntry;
        if (target is null)
        {
            StatusMessage = "请先选中一行流水。";
            return;
        }

        var message = $"确定删除这条流水吗？\n\n{target.DateText} {target.TypeText} {target.AmountText} {target.Category}\n{target.Description}";
        if (!_dialogService.Confirm(message, "删除班费流水"))
        {
            return;
        }

        var result = await _expenseRepository.DeleteAsync(target.Id);
        if (!result.Success)
        {
            StatusMessage = result.ErrorMessage ?? "删除失败。";
            return;
        }

        await ReloadAsync();
        StatusMessage = $"已删除一条流水（{target.Category} {target.AmountText}）。";
    }

    /// <summary>导出当前过滤条件下的账单。</summary>
    [RelayCommand]
    private async Task ExportAsync()
    {
        var (start, end) = GetFilterRange();
        var entries = await _expenseRepository.GetEntriesAsync(start, end, SelectedTypeOption?.Type, SelectedCategoryOption?.Category, Keyword);
        var summary = await _expenseRepository.GetSummaryAsync(start, end, SelectedTypeOption?.Type, SelectedCategoryOption?.Category, Keyword);

        StatusMessage = "正在导出…";
        var result = await _exportService.ExportAsync(start, end, summary, entries);
        StatusMessage = result.Success
            ? $"已导出：{result.FilePath}"
            : result.ErrorMessage ?? "导出失败。";
    }

    partial void OnFilterStartChanged(DateTime? value)
    {
        if (!_suppressReload)
        {
            _ = ReloadAsync();
        }
    }

    partial void OnFilterEndChanged(DateTime? value)
    {
        if (!_suppressReload)
        {
            _ = ReloadAsync();
        }
    }

    partial void OnSelectedTypeOptionChanged(ExpenseOption? value)
    {
        if (!_suppressReload)
        {
            _ = ReloadAsync();
        }
    }

    partial void OnSelectedCategoryOptionChanged(ExpenseOption? value)
    {
        if (!_suppressReload)
        {
            _ = ReloadAsync();
        }
    }

    partial void OnKeywordChanged(string value)
    {
        if (!_suppressReload)
        {
            _ = ReloadAsync();
        }
    }

    private async Task InitializeAsync()
    {
        await ReloadCategoriesAsync();
        await ReloadAsync();
    }

    private async Task ReloadCategoriesAsync()
    {
        _editableCategories = await _expenseRepository.GetCategoriesAsync();
        _lastHandler = await _expenseRepository.GetLastHandlerAsync();

        var options = new List<ExpenseOption> { new("全部类别") };
        foreach (var category in _editableCategories)
        {
            options.Add(new ExpenseOption(category, null, category));
        }

        var previous = SelectedCategoryOption?.Category;
        CategoryOptions = new ObservableCollection<ExpenseOption>(options);
        SelectedCategoryOption = CategoryOptions[0];
        foreach (var option in CategoryOptions)
        {
            if (option.Category == previous)
            {
                SelectedCategoryOption = option;
                break;
            }
        }
    }

    private async Task ReloadAsync()
    {
        var (start, end) = GetFilterRange();

        try
        {
            var entries = await _expenseRepository.GetEntriesAsync(start, end, SelectedTypeOption?.Type, SelectedCategoryOption?.Category, Keyword);
            var summary = await _expenseRepository.GetSummaryAsync(start, end, SelectedTypeOption?.Type, SelectedCategoryOption?.Category, Keyword);

            Entries = new ObservableCollection<ExpenseRecordInfo>(entries);
            TotalIncomeText = summary.TotalIncome.ToString("N2", CultureInfo.CurrentCulture);
            TotalExpenseText = summary.TotalExpense.ToString("N2", CultureInfo.CurrentCulture);
            BalanceText = summary.Balance.ToString("N2", CultureInfo.CurrentCulture);
            EntryCountText = summary.EntryCount.ToString(CultureInfo.CurrentCulture);
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载班费流水失败：{ex.Message}";
        }
    }

    private (DateOnly Start, DateOnly End) GetFilterRange()
    {
        var start = DateOnly.FromDateTime(FilterStart ?? DateTime.Today);
        var end = DateOnly.FromDateTime(FilterEnd ?? DateTime.Today);
        return end < start ? (end, start) : (start, end);
    }
}
