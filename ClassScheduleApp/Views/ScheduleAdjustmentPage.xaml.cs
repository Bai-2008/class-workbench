using System.Linq;
using System.Windows.Controls;
using ClassScheduleApp.ViewModels;

namespace ClassScheduleApp.Views;

/// <summary>
/// 调休补课模块页（纯视图）：数据来自 ScheduleAdjustmentViewModel，后台仅负责把列表的多选项交给 VM。
/// </summary>
public partial class ScheduleAdjustmentPage : UserControl
{
    public ScheduleAdjustmentPage()
    {
        InitializeComponent();
    }

    /// <summary>批量删除所选调整记录（确认 / 备份 / 落库都在 VM）。</summary>
    private void DeleteSelectedAdjustments_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not ScheduleAdjustmentViewModel viewModel)
        {
            return;
        }

        var selected = AdjustmentList.SelectedItems.OfType<ScheduleAdjustmentRowViewModel>().ToList();
        viewModel.DeleteSelectedCommand.Execute(selected);
    }

    /// <summary>没有选中项时「删除所选」不可执行（空选择保护）。</summary>
    private void AdjustmentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DeleteSelectedAdjustmentsButton.IsEnabled = AdjustmentList.SelectedItems.Count > 0;
    }
}
