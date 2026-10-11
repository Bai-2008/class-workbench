using System.Linq;
using System.Windows.Controls;
using ClassScheduleApp.Repositories;
using ClassScheduleApp.ViewModels;

namespace ClassScheduleApp.Views;

/// <summary>
/// 综测记录页。DataContext 由隐式 DataTemplate 继承为 ScoreEntryViewModel，
/// 构造函数零注入；后台仅负责把明细表的多选项交给 ViewModel（确认 / 备份 / 落库都在 VM）。
/// </summary>
public partial class ScoreEntryPage : UserControl
{
    public ScoreEntryPage()
    {
        InitializeComponent();
    }

    /// <summary>批量删除所选综测记录。</summary>
    private void DeleteSelectedEntries_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not ScoreEntryViewModel viewModel)
        {
            return;
        }

        var selected = EntryList.SelectedItems.OfType<ScoreEntryInfo>().ToList();
        viewModel.DeleteSelectedCommand.Execute(selected);
    }

    /// <summary>没有选中项时「删除所选」不可执行（空选择保护）。</summary>
    private void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DeleteSelectedEntriesButton.IsEnabled = EntryList.SelectedItems.Count > 0;
    }
}
