namespace ClassScheduleApp.Services.ScheduleImport;

/// <summary>
/// 课表文本解析器：输入教务导出课表的**文本内容**，输出学生头部信息 + 课程时段列表。
/// 纯函数、无状态、无网络；不做任何猜测式补全，无法识别即明确报告。
/// </summary>
public interface IScheduleTextParser
{
    /// <summary>解析文本；返回<see cref="ParsedSchedule"/>（含跳过行与原因）。</summary>
    ParsedSchedule Parse(string content);
}
