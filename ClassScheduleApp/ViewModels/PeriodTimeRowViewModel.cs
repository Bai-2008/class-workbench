using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 设置页「作息时间」里的一行：第 N 节的开始/结束时刻（文本输入，保存时统一解析校验）。
/// </summary>
public partial class PeriodTimeRowViewModel : ObservableObject
{
    private static readonly string[] TimeFormats = { "HH:mm", "H:mm" };

    public PeriodTimeRowViewModel(int periodNo, TimeOnly start, TimeOnly end)
    {
        PeriodNo = periodNo;
        _startText = Format(start);
        _endText = Format(end);
    }

    /// <summary>节次号（1–12）。</summary>
    public int PeriodNo { get; }

    /// <summary>行标签。</summary>
    public string PeriodLabel => $"第 {PeriodNo} 节";

    /// <summary>开始时刻文本（HH:mm）。</summary>
    [ObservableProperty]
    private string _startText;

    /// <summary>结束时刻文本（HH:mm）。</summary>
    [ObservableProperty]
    private string _endText;

    /// <summary>本行校验提示；为空表示本行合法。</summary>
    [ObservableProperty]
    private string? _error;

    /// <summary>解析本行；失败时写入 <see cref="Error"/> 并返回 false。</summary>
    public bool TryBuild(out TimeOnly start, out TimeOnly end)
    {
        start = default;
        end = default;

        if (!TimeOnly.TryParseExact(StartText.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
        {
            Error = "开始时间格式应为 HH:mm，例如 08:30。";
            return false;
        }

        if (!TimeOnly.TryParseExact(EndText.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out end))
        {
            Error = "结束时间格式应为 HH:mm，例如 09:15。";
            return false;
        }

        if (end <= start)
        {
            Error = "结束时间必须晚于开始时间。";
            return false;
        }

        Error = null;
        return true;
    }

    private static string Format(TimeOnly time)
    {
        return time.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
