using ClassScheduleApp.Services.ScheduleImport;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 单个导入预览里的一行课程时段（可逐行剔除）。
/// </summary>
public partial class ImportEntryRowViewModel : ObservableObject
{
    public ImportEntryRowViewModel(ParsedScheduleEntry entry)
    {
        Entry = entry;
    }

    /// <summary>解析出的时段数据。</summary>
    public ParsedScheduleEntry Entry { get; }

    /// <summary>星期文本。</summary>
    public string WeekdayText => Entry.WeekdayText;

    /// <summary>节次文本。</summary>
    public string PeriodText => Entry.PeriodText;

    /// <summary>周次文本（含单双周）。</summary>
    public string WeekText => Entry.WeekText + (Entry.ParityText.Length > 0 ? " " + Entry.ParityText : string.Empty);

    /// <summary>课程名。</summary>
    public string CourseName => Entry.CourseName;

    /// <summary>教师。</summary>
    public string Teacher => Entry.Teacher;

    /// <summary>教室。</summary>
    public string Location => Entry.Location;

    /// <summary>是否导入本行（取消勾选即剔除）。</summary>
    [ObservableProperty]
    private bool _isIncluded = true;
}
