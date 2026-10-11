using ClassScheduleApp.Models;

namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 调整类型下拉项（调课由「停课 + 补课」两条记录组成，只在弹窗里作为一个选项）。
/// </summary>
public sealed class AdjustmentKindOption
{
    public AdjustmentKindOption(int kind, string text)
    {
        Kind = kind;
        Text = text;
    }

    /// <summary>类型值，见 <see cref="ScheduleAdjustmentTypes"/>。</summary>
    public int Kind { get; }

    /// <summary>显示文本。</summary>
    public string Text { get; }

    /// <summary>供下拉读取显示文本（WPF 的项名称取 ToString，避免显示类型全名）。</summary>
    public override string ToString() => Text;
}
