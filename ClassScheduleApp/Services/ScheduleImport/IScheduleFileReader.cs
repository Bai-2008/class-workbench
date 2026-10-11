namespace ClassScheduleApp.Services.ScheduleImport;

/// <summary>
/// 课表文件读取器：把 .pdf / .txt 文件读成纯文本，交给解析器。
/// 全部本地完成（PDF 用 Docnet.Core 内置的 PDFium，离线、无网络请求）。
/// </summary>
public interface IScheduleFileReader
{
    /// <summary>支持的文件类型（选择文件对话框用）。</summary>
    string FileFilter { get; }

    /// <summary>是否支持该扩展名。</summary>
    bool IsSupported(string filePath);

    /// <summary>读取文件文本；失败抛出 <see cref="IOException"/> 或 <see cref="NotSupportedException"/>。</summary>
    string ReadText(string filePath);
}
