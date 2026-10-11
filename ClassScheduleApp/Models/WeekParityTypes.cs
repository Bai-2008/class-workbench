namespace ClassScheduleApp.Models;

/// <summary>
/// 单双周取值常量。
/// 语义：以**学期教学周序号**为准（第 1、3、5… 周为单周），周序号一律经 ISemesterService 换算。
/// </summary>
public static class WeekParityTypes
{
    /// <summary>每周（不做奇偶过滤）。</summary>
    public const int Every = 0;

    /// <summary>单周（第 1、3、5… 周）。</summary>
    public const int Odd = 1;

    /// <summary>双周（第 2、4、6… 周）。</summary>
    public const int Even = 2;

    /// <summary>下拉展示顺序。</summary>
    public static IReadOnlyList<string> AllDisplay { get; } = new[] { "每周", "单周", "双周" };

    /// <summary>展示文本 → 存储值。</summary>
    public static int FromDisplay(string? displayText)
    {
        return displayText switch
        {
            "单周" => Odd,
            "双周" => Even,
            _ => Every,
        };
    }

    /// <summary>存储值 → 展示文本。</summary>
    public static string ToDisplay(int parity)
    {
        return parity switch
        {
            Odd => "单周",
            Even => "双周",
            _ => "每周",
        };
    }

    /// <summary>
    /// 该时段在第 <paramref name="week"/> 周是否生效 —— 全项目唯一的周次判定入口。
    /// 网格、列表、空闲统计必须共用此方法，禁止各写一份谓词。
    /// 调休补课产生的补课记录不走此处（调整是日期锚定的，不做奇偶过滤）。
    /// </summary>
    public static bool IsActiveInWeek(int startWeek, int endWeek, int weekParity, int week)
    {
        if (week < startWeek || week > endWeek)
        {
            return false;
        }

        return MatchesParity(weekParity, week);
    }

    /// <summary>奇偶是否匹配（每周恒为 true）。</summary>
    public static bool MatchesParity(int weekParity, int week)
    {
        return weekParity switch
        {
            Odd => week % 2 == 1,
            Even => week % 2 == 0,
            _ => true,
        };
    }
}
