namespace ClassScheduleApp.Services;

/// <summary>
/// 剪贴板服务：封装系统剪贴板读写，供各模块复用（View/ViewModel 不直接碰 System.Windows）。
/// </summary>
public interface IClipboardService
{
    /// <summary>写入文本；被其它进程占用等情况下返回 false，不抛异常。</summary>
    bool TrySetText(string text);

    /// <summary>读取文本；失败或无文本时返回 null。</summary>
    string? TryGetText();
}
