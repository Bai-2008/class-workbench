using System.Windows.Controls;

namespace ClassScheduleApp.Views;

/// <summary>
/// 考勤管理页。DataContext 由隐式 DataTemplate 继承为 AttendanceViewModel，
/// 构造函数零注入、后台零逻辑。
/// </summary>
public partial class AttendancePage : UserControl
{
    public AttendancePage()
    {
        InitializeComponent();
    }
}
