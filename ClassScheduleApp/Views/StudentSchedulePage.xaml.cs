using System.Linq;
using System.Windows.Controls;
using ClassScheduleApp.Models;
using ClassScheduleApp.ViewModels;

namespace ClassScheduleApp.Views;

/// <summary>
/// 学生课表页。只做布局与数据绑定，DataContext 由隐式模板注入为 StudentScheduleViewModel。
/// </summary>
public partial class StudentSchedulePage : UserControl
{
    public StudentSchedulePage()
    {
        InitializeComponent();
    }

    /// <summary>批量删除所选学生：把列表的多选项交给 ViewModel 处理（确认框 / 备份 / 落库都在 VM）。</summary>
    private void DeleteSelectedStudents_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not StudentScheduleViewModel viewModel)
        {
            return;
        }

        var selected = StudentList.SelectedItems.OfType<Student>().ToList();
        viewModel.DeleteStudentsCommand.Execute(selected);
    }

    /// <summary>没有选中学生时「删除所选」不可执行（批量删除的空选择保护）。</summary>
    private void StudentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DeleteSelectedButton.IsEnabled = StudentList.SelectedItems.Count > 0;
    }
}
