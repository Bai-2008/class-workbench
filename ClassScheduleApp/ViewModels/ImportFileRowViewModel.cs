using System.Collections.ObjectModel;
using ClassScheduleApp.Models;
using ClassScheduleApp.Services.ScheduleImport;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 批量导入清单里的一行（一个文件）。
/// </summary>
public partial class ImportFileRowViewModel : ObservableObject
{
    public ImportFileRowViewModel(ImportFilePreview preview)
    {
        Preview = preview;
        Candidates = new ObservableCollection<Student>(preview.Candidates);
        _selectedStudent = preview.BoundStudent;
        _isIncluded = preview.IsUsable;
    }

    /// <summary>该文件的解析与匹配预览。</summary>
    public ImportFilePreview Preview { get; }

    /// <summary>文件名。</summary>
    public string FileName => Preview.FileName;

    /// <summary>可选学生（自动匹配成功时只有 1 个；未匹配/重名时供人工选择）。</summary>
    public ObservableCollection<Student> Candidates { get; }

    /// <summary>课程数。</summary>
    public int CourseCount => Preview.CourseCount;

    /// <summary>异常行数。</summary>
    public int SkipCount => Preview.SkipCount;

    /// <summary>匹配状态文本。</summary>
    public string MatchText => Preview.MatchText;

    /// <summary>问题说明（解析失败原因 / 重名 / 未匹配）。</summary>
    public string NoteText
    {
        get
        {
            if (Preview.FailureReason is not null)
            {
                return Preview.FailureReason;
            }

            return Preview.MatchState switch
            {
                ImportMatchState.Ambiguous => "姓名重名，请选择学生",
                ImportMatchState.NotFound => "未匹配到学生，请选择",
                _ => string.Empty,
            };
        }
    }

    /// <summary>同一学生多份文件时的「以最后一份为准」标注。</summary>
    [ObservableProperty]
    private string _lastOneNote = string.Empty;

    /// <summary>是否参与本次导入。</summary>
    [ObservableProperty]
    private bool _isIncluded;

    /// <summary>人工绑定/改绑的学生。</summary>
    [ObservableProperty]
    private Student? _selectedStudent;
}
