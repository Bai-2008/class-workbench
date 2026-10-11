using System.Windows.Controls;

namespace ClassScheduleApp.Views.Dialogs;

/// <summary>
/// 调整弹窗视图：DataContext 由 DialogShell 的隐式模板继承，后台零逻辑。
/// </summary>
public partial class ScheduleAdjustmentEditView : UserControl
{
    public ScheduleAdjustmentEditView()
    {
        InitializeComponent();
    }
}
