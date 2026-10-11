using System.Windows;

namespace ClassScheduleApp.Views.Dialogs;

/// <summary>
/// 通用弹窗壳：只含标题、内容区、确定/取消三部分。
/// 内容由本窗口资源段里的隐式 DataTemplate 按 ViewModel 类型渲染；
/// 尺寸由各编辑视图内容自适应撑开；关闭由 DialogService 监听 DialogResult 完成，
/// 因此后台零逻辑。
/// </summary>
public partial class DialogShell : Window
{
    public DialogShell()
    {
        InitializeComponent();
    }
}
