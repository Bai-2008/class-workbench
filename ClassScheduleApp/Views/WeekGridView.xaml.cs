using System.Windows.Controls;

namespace ClassScheduleApp.Views;

/// <summary>
/// 周视图网格（纯视图）：数据来自 StudentScheduleViewModel.WeekGridItems，后台零逻辑。
/// </summary>
public partial class WeekGridView : UserControl
{
    public WeekGridView()
    {
        InitializeComponent();
    }
}
