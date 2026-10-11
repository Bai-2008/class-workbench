using System.Windows.Controls;

namespace ClassScheduleApp.Views.Dialogs;

/// <summary>
/// 冲突候选课程选择视图：DataContext 由 DialogShell 的隐式模板继承，后台零逻辑。
/// </summary>
public partial class CourseChoiceView : UserControl
{
    public CourseChoiceView()
    {
        InitializeComponent();
    }
}
