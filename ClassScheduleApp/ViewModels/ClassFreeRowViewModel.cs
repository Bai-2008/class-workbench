namespace ClassScheduleApp.ViewModels;

/// <summary>
/// 空闲矩阵的一行（页面用）：一个节次对 × 7 天。
/// 42 格数据以 6 行 × 7 列的方式组织，便于表格化布局。
/// </summary>
public sealed class ClassFreeRowViewModel
{
    public ClassFreeRowViewModel(string slotText, IReadOnlyList<ClassFreeCellViewModel> cells)
    {
        SlotText = slotText;
        Cells = cells;
    }

    /// <summary>行标题，如 1-2节。</summary>
    public string SlotText { get; }

    /// <summary>该行的 7 个格子（周一 → 周日）。</summary>
    public IReadOnlyList<ClassFreeCellViewModel> Cells { get; }
}
