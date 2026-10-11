using System.Windows.Controls;

namespace ClassScheduleApp.Views;

/// <summary>
/// 空闲时间统计页。DataContext 由隐式 DataTemplate 继承为 FreeTimeStatsViewModel，
/// 构造函数零注入、后台零逻辑。
/// </summary>
public partial class FreeTimeStatsPage : UserControl
{
    public FreeTimeStatsPage()
    {
        InitializeComponent();
    }
}
