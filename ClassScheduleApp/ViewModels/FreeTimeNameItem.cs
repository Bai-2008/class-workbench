namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 名单项（空闲名单 / 有课名单共用）：显示"姓名（学号）+ 可选补充（课程 教室）"。
/// </summary>
public sealed class FreeTimeNameItem
{
    public FreeTimeNameItem(string studentNo, string name, string? detail)
    {
        StudentNo = studentNo;
        Name = name;
        Detail = detail;
    }

    /// <summary>学号。</summary>
    public string StudentNo { get; }

    /// <summary>姓名。</summary>
    public string Name { get; }

    /// <summary>补充信息，空闲名单为空；有课名单为"课程名 教室"。</summary>
    public string? Detail { get; }

    /// <summary>列表显示文本。</summary>
    public string DisplayText => string.IsNullOrEmpty(Detail)
        ? $"{Name}（{StudentNo}）"
        : $"{Name}（{StudentNo}）  {Detail}";

    /// <summary>供下拉读取显示文本（WPF 的项名称取 ToString，避免显示类型全名）。</summary>
    public override string ToString() => DisplayText;
}
