using ClassScheduleApp.Models;
using ClassScheduleApp.Repositories;

namespace ClassScheduleApp.Services;

/// <summary>
/// 对话框服务：由 ViewModel 调用，避免 ViewModel 直接引用 System.Windows。
/// 实现放在表现层（Views/DialogService.cs）。
/// </summary>
public interface IDialogService
{
    /// <summary>打开学生新增/编辑窗口；student 为 null 表示新增。返回 true 表示已保存。</summary>
    bool ShowStudentEditor(Student? student);

    /// <summary>打开课程管理窗口（显示前已完成数据加载）。返回 true 表示课程数据有变化。</summary>
    Task<bool> ShowCourseManagerAsync();

    /// <summary>打开课程新增/编辑窗口；course 为 null 表示新增。返回 true 表示已保存。</summary>
    bool ShowCourseEditor(CourseDetail? course);

    /// <summary>打开选课窗口（显示前已完成课程与已选状态加载）。返回 true 表示选课关系有变化。</summary>
    Task<bool> ShowEnrollmentAsync(Student student);

    /// <summary>确认框，用户选择"是"返回 true。</summary>
    bool Confirm(string message, string title);

    /// <summary>错误提示。</summary>
    void ShowError(string message, string title);

    /// <summary>
    /// 通用弹窗：用统一弹窗壳承载指定弹窗 ViewModel。
    /// 内容由弹窗壳资源段里的隐式模板按 ViewModel 类型渲染；确定/取消绑定 ViewModel 的命令；
    /// 宿主用当前活动窗口（回退 <c>Application.Current.MainWindow</c>）。
    /// 返回 true = 用户点了确定，false/null = 取消。
    /// </summary>
    bool? ShowDialog<TVm>(TVm viewModel)
        where TVm : IDialogViewModel;
}
