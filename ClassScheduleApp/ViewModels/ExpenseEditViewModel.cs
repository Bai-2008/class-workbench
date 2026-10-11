using System.Globalization;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 班费流水的编辑弹窗 ViewModel：只收集与校验输入，不落库（持久化由 ClassFundsViewModel 完成）。
/// 校验：金额必须是大于 0 的数字并按两位小数四舍五入、说明非空、类别非空；
/// 不满足时确定按钮禁用（CanExecute=false）并给出提示。
/// </summary>
public partial class ExpenseEditViewModel : DialogViewModelBase
{
    /// <summary>编辑结果，确定后由 <see cref="BuildInput"/> 生成。</summary>
    public ExpenseInput? Result { get; private set; }

    /// <summary>类型下拉。</summary>
    public IReadOnlyList<ExpenseOption> TypeOptions { get; } = new List<ExpenseOption>
    {
        new("收入", ExpenseType.Income),
        new("支出", ExpenseType.Expense),
    };

    /// <summary>类别下拉（可编辑，允许自定义输入）。</summary>
    [ObservableProperty]
    private IReadOnlyList<string> _categoryOptions = new List<string>();

    /// <summary>发生日期。</summary>
    [ObservableProperty]
    private DateTime? _date = DateTime.Today;

    /// <summary>类型。</summary>
    [ObservableProperty]
    private ExpenseOption _selectedTypeOption;

    /// <summary>金额（文本，便于校验非法输入）。</summary>
    [ObservableProperty]
    private string _amountText = string.Empty;

    /// <summary>类别。</summary>
    [ObservableProperty]
    private string _category = string.Empty;

    /// <summary>说明。</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>经办人（默认取上次使用的值）。</summary>
    [ObservableProperty]
    private string _handler = string.Empty;

    /// <summary>备注。</summary>
    [ObservableProperty]
    private string _note = string.Empty;

    /// <summary>校验提示：不满足时是错误原因，满足但有取整时是取整提示。</summary>
    [ObservableProperty]
    private string _message = string.Empty;

    public ExpenseEditViewModel()
    {
        _selectedTypeOption = TypeOptions[0];
    }

    /// <summary>
    /// 初始化：existing 为 null 表示新增。categories 为类别下拉数据，lastHandler 为上次使用的经办人。
    /// </summary>
    public void Initialize(ExpenseRecordInfo? existing, IReadOnlyList<string> categories, string? lastHandler)
    {
        CategoryOptions = categories;
        Title = existing is null ? "记一笔班费" : "编辑班费流水";

        Date = existing is null
            ? DateTime.Today
            : existing.Date.ToDateTime(TimeOnly.MinValue).Date;

        SelectedTypeOption = existing?.Type == ExpenseType.Expense ? TypeOptions[1] : TypeOptions[0];
        AmountText = existing is null ? string.Empty : existing.Amount.ToString("0.##", CultureInfo.CurrentCulture);
        Category = existing is null
            ? (categories.Count > 0 ? categories[0] : string.Empty)
            : existing.Category;
        Description = existing?.Description ?? string.Empty;
        Handler = existing is null ? (lastHandler ?? string.Empty) : existing.Handler;
        Note = existing?.Note ?? string.Empty;

        RefreshValidation();
    }

    /// <summary>确定后生成输入数据（校验已通过）。</summary>
    protected override void OnConfirm()
    {
        Result = BuildInput();
    }

    /// <inheritdoc />
    protected override bool CanConfirm()
    {
        return TryBuildAmount(out _) &&
               !string.IsNullOrWhiteSpace(Description) &&
               !string.IsNullOrWhiteSpace(Category);
    }

    private ExpenseInput BuildInput()
    {
        TryBuildAmount(out var amount);
        var description = Description.Trim();
        var category = Category.Trim();
        var handler = Handler.Trim();
        var note = Note.Trim();

        return new ExpenseInput(
            DateOnly.FromDateTime(Date ?? DateTime.Today),
            SelectedTypeOption.Type ?? ExpenseType.Income,
            amount,
            category,
            description,
            handler,
            note.Length == 0 ? null : note);
    }

    /// <summary>解析并四舍五入到两位小数；失败或 &lt;= 0 返回 false。</summary>
    private bool TryBuildAmount(out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(AmountText))
        {
            return false;
        }

        if (!decimal.TryParse(AmountText.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var raw))
        {
            return false;
        }

        var rounded = Math.Round(raw, 2, MidpointRounding.AwayFromZero);
        if (rounded <= 0m)
        {
            return false;
        }

        amount = rounded;
        return true;
    }

    /// <summary>金额是否需要四舍五入提示。</summary>
    private bool HasRoundingChange(out decimal rounded)
    {
        rounded = 0m;
        if (string.IsNullOrWhiteSpace(AmountText))
        {
            return false;
        }

        if (!decimal.TryParse(AmountText.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var raw))
        {
            return false;
        }

        rounded = Math.Round(raw, 2, MidpointRounding.AwayFromZero);
        return rounded != raw;
    }

    private void RefreshValidation()
    {
        if (HasRoundingChange(out var rounded))
        {
            Message = $"金额将按两位小数记为 {rounded.ToString("N2", CultureInfo.CurrentCulture)}。";
        }
        else
        {
            Message = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(AmountText))
        {
            Message = "请填写金额。";
        }
        else if (!TryBuildAmount(out _))
        {
            Message = "金额必须是大于 0 的数字（多余小数位会四舍五入到两位）。";
        }
        else if (string.IsNullOrWhiteSpace(Description))
        {
            Message = "说明不能为空。";
        }
        else if (string.IsNullOrWhiteSpace(Category))
        {
            Message = "类别不能为空：可从下拉选择，也可自己输入。";
        }

        RaiseCanConfirmChanged();
    }

    partial void OnAmountTextChanged(string value)
    {
        RefreshValidation();
    }

    partial void OnDescriptionChanged(string value)
    {
        RefreshValidation();
    }

    partial void OnCategoryChanged(string value)
    {
        RefreshValidation();
    }

    partial void OnSelectedTypeOptionChanged(ExpenseOption value)
    {
        RefreshValidation();
    }
}
