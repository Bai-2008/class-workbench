using System.Windows.Controls;

namespace ClassScheduleApp.Views;

/// <summary>
/// 班费记账页。DataContext 由隐式 DataTemplate 继承为 ClassFundsViewModel，
/// 构造函数零注入、后台零逻辑。
/// </summary>
public partial class ClassFundsPage : UserControl
{
    public ClassFundsPage()
    {
        InitializeComponent();
    }
}
