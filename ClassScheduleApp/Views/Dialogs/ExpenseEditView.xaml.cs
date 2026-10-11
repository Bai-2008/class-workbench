using System.Windows.Controls;

namespace ClassScheduleApp.Views.Dialogs;

/// <summary>
/// 班费流水编辑视图：DataContext 由 DialogShell 的隐式模板继承，后台零逻辑。
/// </summary>
public partial class ExpenseEditView : UserControl
{
    public ExpenseEditView()
    {
        InitializeComponent();
    }
}
