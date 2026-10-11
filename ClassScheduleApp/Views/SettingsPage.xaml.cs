using System.Windows.Controls;

namespace ClassScheduleApp.Views;

/// <summary>
/// 设置页。只做布局与数据绑定，DataContext 由外壳（MainWindow）注入为 SettingsViewModel。
/// </summary>
public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }
}
