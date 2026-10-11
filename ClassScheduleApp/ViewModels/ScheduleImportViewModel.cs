using System.Collections.ObjectModel;
using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;
using ClassScheduleApp.Services.ScheduleImport;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 「课表导入」模块页：单个导入（粘贴文本/单个文件 → 预览可剔除 → 确认）
/// 与批量导入（多选文件 → 解析核对清单可改绑/剔除 → 全部导入 → 报告）。
/// 解析、匹配、落库全部本地完成。
/// </summary>
public partial class ScheduleImportViewModel : ObservableObject
{
    private readonly IScheduleImportService _importService;
    private readonly IScheduleFileReader _fileReader;
    private readonly IFreeTimeRepository _freeTimeRepository;

    public ScheduleImportViewModel(
        IScheduleImportService importService,
        IScheduleFileReader fileReader,
        IFreeTimeRepository freeTimeRepository)
    {
        _importService = importService;
        _fileReader = fileReader;
        _freeTimeRepository = freeTimeRepository;

        _ = InitializeAsync();
    }

    /// <summary>可导入的学期列表。</summary>
    public ObservableCollection<string> Semesters { get; } = new();

    /// <summary>当前学期。</summary>
    [ObservableProperty]
    private string? _selectedSemester;

    /// <summary>单个导入：粘贴的课表文本。</summary>
    [ObservableProperty]
    private string _singleText = string.Empty;

    /// <summary>单个导入：解析出的行。</summary>
    public ObservableCollection<ImportEntryRowViewModel> SingleEntries { get; } = new();

    /// <summary>单个导入：学生候选。</summary>
    public ObservableCollection<Student> SingleCandidates { get; } = new();

    /// <summary>单个导入：已绑定学生。</summary>
    [ObservableProperty]
    private Student? _singleStudent;

    /// <summary>单个导入：状态文本。</summary>
    [ObservableProperty]
    private string _singleStatus = "粘贴课表文本或选择文件后点「解析预览」。";

    /// <summary>批量导入：清单。</summary>
    public ObservableCollection<ImportFileRowViewModel> BatchFiles { get; } = new();

    /// <summary>批量导入：状态文本。</summary>
    [ObservableProperty]
    private string _batchStatus = "选择多个课表文件后自动解析并匹配学生。";

    /// <summary>导入报告。</summary>
    [ObservableProperty]
    private string _reportText = string.Empty;

    /// <summary>是否正在处理（禁用按钮）。</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>加载学期列表。</summary>
    private async Task InitializeAsync()
    {
        try
        {
            var semesters = await _freeTimeRepository.GetSemestersAsync();
            Semesters.Clear();
            foreach (var semester in semesters)
            {
                Semesters.Add(semester);
            }

            SelectedSemester = Semesters.Count > 0 ? Semesters[0] : null;
        }
        catch (Exception ex)
        {
            SingleStatus = $"加载学期失败：{ex.Message}";
        }
    }

    /// <summary>解析粘贴的文本。</summary>
    [RelayCommand]
    private async Task ParseTextAsync()
    {
        if (string.IsNullOrWhiteSpace(SingleText))
        {
            SingleStatus = "请先粘贴课表文本。";
            return;
        }

        IsBusy = true;
        try
        {
            var preview = await _importService.PreviewTextAsync(SingleText, "粘贴的文本");
            ApplySinglePreview(preview);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>选择单个文件并解析。</summary>
    [RelayCommand]
    private async Task PickSingleFileAsync()
    {
        var dialog = new OpenFileDialog { Filter = _fileReader.FileFilter, Title = "选择课表文件" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var preview = await _importService.PreviewFileAsync(dialog.FileName);
            ApplySinglePreview(preview);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>确认导入（覆盖该生本学期课表）。</summary>
    [RelayCommand]
    private async Task ConfirmSingleAsync()
    {
        if (SingleStudent is null)
        {
            SingleStatus = "请先选择学生。";
            return;
        }

        if (SelectedSemester is null)
        {
            SingleStatus = "请先选择学期。";
            return;
        }

        var included = SingleEntries.Where(r => r.IsIncluded).Select(r => r.Entry).ToList();
        if (included.Count == 0)
        {
            SingleStatus = "没有可导入的行（全部被剔除）。";
            return;
        }

        var preview = new ImportFilePreview
        {
            FilePath = _lastSinglePreview?.FilePath ?? string.Empty,
            FileName = string.IsNullOrWhiteSpace(SingleSourceName) ? "粘贴的文本" : SingleSourceName,
            Parsed = new ParsedSchedule
            {
                Header = _lastSinglePreview?.Parsed.Header ?? new ParsedScheduleHeader(),
                Entries = included,
                Skipped = _lastSinglePreview?.Parsed.Skipped ?? new List<ParsedScheduleSkip>(),
                IsRecognized = true,
            },
        };

        IsBusy = true;
        try
        {
            var report = await _importService.ImportAsync(
                new[] { (preview, SingleStudent.Id) },
                SelectedSemester);
            ReportText = report.ToText();
            SingleStatus = $"已导入 {included.Count} 个时段（覆盖 {SingleStudent.Name} 在 {SelectedSemester} 的课表）。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>批量：选择多个文件并解析。</summary>
    [RelayCommand]
    private async Task PickFilesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = _fileReader.FileFilter,
            Title = "选择课表文件（可多选）",
            Multiselect = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsBusy = true;
        try
        {
            BatchFiles.Clear();
            foreach (var file in dialog.FileNames)
            {
                var preview = await _importService.PreviewFileAsync(file);
                var row = new ImportFileRowViewModel(preview);
                // 改绑学生后重算「以最后一份为准」标注
                row.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(ImportFileRowViewModel.SelectedStudent))
                    {
                        RefreshLastOneNotes();
                    }
                };
                BatchFiles.Add(row);
            }

            RefreshLastOneNotes();
            BatchStatus = $"已解析 {BatchFiles.Count} 个文件：可导入 {BatchFiles.Count(f => f.IsIncluded)} 个，异常 {BatchFiles.Count(f => !f.IsIncluded)} 个。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>剔除批量清单里的某一行。</summary>
    [RelayCommand]
    private void RemoveBatchFile(ImportFileRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        BatchFiles.Remove(row);
        RefreshLastOneNotes();
        BatchStatus = $"已从清单移除「{row.FileName}」，剩余 {BatchFiles.Count} 个文件。";
    }

    /// <summary>批量：全部导入（单一确认后逐文件执行）。</summary>
    [RelayCommand]
    private async Task ImportAllAsync()
    {
        if (SelectedSemester is null)
        {
            BatchStatus = "请先选择学期。";
            return;
        }

        var items = new List<(ImportFilePreview Preview, int StudentId)>();
        var unbound = new List<string>();

        foreach (var row in BatchFiles.Where(f => f.IsIncluded))
        {
            if (row.SelectedStudent is null)
            {
                unbound.Add(row.FileName);
                continue;
            }

            items.Add((row.Preview, row.SelectedStudent.Id));
        }

        if (unbound.Count > 0)
        {
            BatchStatus = $"以下文件未绑定学生，本次不导入：{string.Join("、", unbound)}";
        }

        if (items.Count == 0)
        {
            BatchStatus = "没有可导入的文件（需勾选且已绑定学生）。";
            return;
        }

        IsBusy = true;
        try
        {
            var report = await _importService.ImportAsync(items, SelectedSemester);
            if (unbound.Count > 0)
            {
                report.Failures.Add($"未绑定学生、未导入：{string.Join("、", unbound)}");
                report.FailedFileCount += unbound.Count;
            }

            ReportText = report.ToText();
            BatchStatus = $"导入结束：成功 {report.SuccessCount} 人，失败 {report.FailedFileCount} 个文件。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string SingleSourceName { get; set; } = string.Empty;

    /// <summary>最近一次单个解析的结果（确认导入时复用其头部与跳过行）。</summary>
    private ImportFilePreview? _lastSinglePreview;

    private void ApplySinglePreview(ImportFilePreview preview)
    {
        _lastSinglePreview = preview;
        SingleSourceName = preview.FileName;
        SingleEntries.Clear();

        foreach (var entry in preview.Parsed.Entries)
        {
            SingleEntries.Add(new ImportEntryRowViewModel(entry));
        }

        SingleCandidates.Clear();
        foreach (var student in preview.Candidates)
        {
            SingleCandidates.Add(student);
        }

        SingleStudent = preview.BoundStudent ?? (SingleCandidates.Count == 1 ? SingleCandidates[0] : null);

        if (!preview.IsUsable)
        {
            SingleStatus = $"解析失败：{preview.FailureReason ?? preview.Parsed.FailureReason ?? "没有可导入的课程行"}";
            return;
        }

        var matchText = preview.MatchState switch
        {
            ImportMatchState.Matched => $"已自动匹配 {preview.BoundStudent?.Name}（{preview.BoundStudent?.StudentNo}）",
            ImportMatchState.Ambiguous => "姓名重名，请在下方选择学生",
            _ => "未匹配到学生，请在下方选择",
        };

        var skipText = preview.SkipCount > 0 ? $"，跳过 {preview.SkipCount} 行（见报告）" : string.Empty;
        SingleStatus = $"解析成功：{preview.CourseCount} 个时段{skipText}；{matchText}。确认导入将覆盖该生本学期课表。";
    }

    /// <summary>同一学生出现多份文件时，除最后一份外都标注「以最后一份为准」。</summary>
    private void RefreshLastOneNotes()
    {
        foreach (var row in BatchFiles)
        {
            row.LastOneNote = string.Empty;
        }

        var groups = BatchFiles
            .Where(f => f.SelectedStudent is not null)
            .GroupBy(f => f.SelectedStudent!.Id);

        foreach (var group in groups)
        {
            var list = group.ToList();
            for (var i = 0; i < list.Count - 1; i++)
            {
                list[i].LastOneNote = "以最后一份为准（将被后续文件覆盖）";
            }
        }
    }
}
