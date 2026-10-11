using System.Collections.ObjectModel;
using System.Globalization;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 综测记录的编辑弹窗 ViewModel：学生多选（带搜索）+ 类别（下拉可自定义）+ 分数 + 说明 + 佐证 + 日期。
/// 只收集与校验输入，不落库（持久化由 ScoreEntryViewModel 完成）。
/// 多选时为每个学生插入一条记录（批量），单选即单人添加。
/// </summary>
public partial class ScoreEditViewModel : DialogViewModelBase
{
    private readonly List<StudentSelectionItem> _allStudents = new();

    /// <summary>是否处于编辑模式（编辑时锁定学生、学期取原记录的值）。</summary>
    public bool IsEditing { get; private set; }

    /// <summary>学期（新增时取页面当前筛选学期；编辑时取原记录学期）。</summary>
    [ObservableProperty]
    private string _semester = string.Empty;

    /// <summary>是否可以更换学生（编辑时不可）。</summary>
    [ObservableProperty]
    private bool _isStudentSelectionEnabled = true;

    /// <summary>学生搜索关键词（匹配姓名或学号）。</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>搜索后可见的学生列表。</summary>
    [ObservableProperty]
    private ObservableCollection<StudentSelectionItem> _visibleStudents = new();

    /// <summary>类别下拉（预设 + 历史自定义，可编辑输入）。</summary>
    [ObservableProperty]
    private IReadOnlyList<string> _categoryOptions = new List<string>();

    /// <summary>类别。</summary>
    [ObservableProperty]
    private string _category = string.Empty;

    /// <summary>分数（文本，可为负）。</summary>
    [ObservableProperty]
    private string _scoreText = string.Empty;

    /// <summary>说明。</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>佐证说明（可空）。</summary>
    [ObservableProperty]
    private string _evidence = string.Empty;

    /// <summary>记录日期。</summary>
    [ObservableProperty]
    private DateTime? _entryDate = DateTime.Today;

    /// <summary>提示：不满足时是错误原因，满足但有取整时是取整提示。</summary>
    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>已勾选人数提示。</summary>
    [ObservableProperty]
    private string _selectionText = "已选 0 人";

    /// <summary>已勾选的学生 Id。</summary>
    public IReadOnlyList<int> SelectedStudentIds
    {
        get
        {
            var ids = new List<int>();
            foreach (var student in _allStudents)
            {
                if (student.IsSelected)
                {
                    ids.Add(student.StudentId);
                }
            }

            return ids;
        }
    }

    /// <summary>
    /// 初始化。existing 为 null 表示新增；students 为全班学生；categories 为类别下拉；semester 为学期。
    /// </summary>
    public void Initialize(
        ScoreEntryInfo? existing,
        IReadOnlyList<Student> students,
        IReadOnlyList<string> categories,
        string semester)
    {
        IsEditing = existing is not null;
        Title = existing is null ? "添加综测记录" : "编辑综测记录";
        Semester = existing?.Semester ?? semester;
        IsStudentSelectionEnabled = existing is null;
        CategoryOptions = categories;

        _allStudents.Clear();
        foreach (var student in students)
        {
            var isTarget = existing is not null && existing.StudentId == student.Id;
            _allStudents.Add(new StudentSelectionItem(student.Id, student.StudentNo, student.Name, RefreshValidation)
            {
                IsSelected = isTarget,
                IsEnabled = existing is null || isTarget,
            });
        }

        SearchText = string.Empty;
        Category = existing?.Category ?? (categories.Count > 0 ? categories[0] : string.Empty);
        ScoreText = existing is null
            ? string.Empty
            : existing.Score.ToString("0.##", CultureInfo.CurrentCulture);
        Description = existing?.Description ?? string.Empty;
        Evidence = existing?.Evidence ?? string.Empty;
        EntryDate = existing is null
            ? DateTime.Today
            : existing.EntryDate.ToDateTime(TimeOnly.MinValue).Date;

        RefreshVisibleStudents();
        RefreshValidation();
    }

    /// <summary>把界面输入组装成记录内容（校验已通过）。</summary>
    public ScoreEntryDraft BuildDraft(string handler)
    {
        TryBuildScore(out var score);
        var evidence = Evidence.Trim();
        return new ScoreEntryDraft(
            Semester.Trim(),
            Category.Trim(),
            score,
            Description.Trim(),
            evidence.Length == 0 ? null : evidence,
            DateOnly.FromDateTime(EntryDate ?? DateTime.Today),
            handler);
    }

    /// <inheritdoc />
    protected override bool CanConfirm()
    {
        return SelectedStudentIds.Count > 0 &&
               !string.IsNullOrWhiteSpace(Semester) &&
               !string.IsNullOrWhiteSpace(Category) &&
               !string.IsNullOrWhiteSpace(Description) &&
               TryBuildScore(out _);
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshVisibleStudents();
    }

    partial void OnCategoryChanged(string value)
    {
        RefreshValidation();
    }

    partial void OnDescriptionChanged(string value)
    {
        RefreshValidation();
    }

    partial void OnScoreTextChanged(string value)
    {
        RefreshValidation();
    }

    /// <summary>解析分数（允许负值），多余小数位四舍五入到两位。</summary>
    private bool TryBuildScore(out decimal score)
    {
        score = 0m;
        if (string.IsNullOrWhiteSpace(ScoreText))
        {
            return false;
        }

        if (!decimal.TryParse(ScoreText.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var raw))
        {
            return false;
        }

        score = Math.Round(raw, 2, MidpointRounding.AwayFromZero);
        return true;
    }

    /// <summary>全选：勾选当前列表里所有可选学生（受搜索过滤与"编辑时锁定"限制）。</summary>
    [RelayCommand]
    private void SelectAll()
    {
        SetVisibleSelected(true);
    }

    /// <summary>全不选：取消勾选当前列表里的学生。</summary>
    [RelayCommand]
    private void SelectNone()
    {
        SetVisibleSelected(false);
    }

    private void SetVisibleSelected(bool value)
    {
        foreach (var student in VisibleStudents)
        {
            if (value && !student.IsEnabled)
            {
                // 编辑已有记录时原学生被锁定，全选不改变它
                continue;
            }

            student.IsSelected = value;
        }

        UpdateSelectionText();
        RaiseCanConfirmChanged();
        RefreshValidation();
    }

    private void RefreshVisibleStudents()
    {
        var keyword = SearchText.Trim();
        var visible = new List<StudentSelectionItem>();
        foreach (var student in _allStudents)
        {
            if (keyword.Length == 0 ||
                student.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                student.StudentNo.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                visible.Add(student);
            }
        }

        VisibleStudents = new ObservableCollection<StudentSelectionItem>(visible);
        UpdateSelectionText();
        RaiseCanConfirmChanged();
    }

    /// <summary>勾选变化后刷新人数与校验状态（由学生项的勾选回调触发）。</summary>
    private void UpdateSelectionText()
    {
        var count = SelectedStudentIds.Count;
        SelectionText = $"已选 {count} 人";
    }

    private void RefreshValidation()
    {
        UpdateSelectionText();

        Message = string.Empty;
        if (SelectedStudentIds.Count == 0)
        {
            Message = IsEditing ? "编辑记录时必须保留原学生。" : "请至少勾选一名学生。";
        }
        else if (string.IsNullOrWhiteSpace(Category))
        {
            Message = "类别不能为空：可从下拉选择，也可自己输入。";
        }
        else if (string.IsNullOrWhiteSpace(ScoreText))
        {
            Message = "请填写分数（可为负，表示扣分）。";
        }
        else if (!TryBuildScore(out _))
        {
            Message = "分数必须是数字（可为负；多余小数位会四舍五入到两位）。";
        }
        else if (string.IsNullOrWhiteSpace(Description))
        {
            Message = "说明不能为空。";
        }
        else if (TryBuildScore(out var rounded) &&
                 decimal.TryParse(ScoreText.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var raw) &&
                 rounded != raw)
        {
            Message = $"分数将按两位小数记为 {rounded.ToString("0.##", CultureInfo.CurrentCulture)}。";
        }

        RaiseCanConfirmChanged();
    }
}
