using System.IO;
using Docnet.Core;
using Docnet.Core.Models;

namespace ClassScheduleApp.Services.ScheduleImport;

/// <summary>
/// 课表文件读取器实现：PDF 走 Docnet.Core（PDFium，本地离线），其余按 UTF-8 文本读取。
/// </summary>
public sealed class ScheduleFileReader : IScheduleFileReader
{
    /// <inheritdoc />
    public string FileFilter => "课表文件 (*.pdf;*.txt)|*.pdf;*.txt|PDF 文件 (*.pdf)|*.pdf|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";

    /// <inheritdoc />
    public bool IsSupported(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public string ReadText(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new IOException($"文件不存在：{filePath}");
        }

        if (Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return ReadPdf(filePath);
        }

        if (Path.GetExtension(filePath).Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return File.ReadAllText(filePath);
        }

        throw new NotSupportedException($"暂不支持的文件类型：{Path.GetExtension(filePath)}（支持 .pdf / .txt）");
    }

    private static string ReadPdf(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);

        using var document = DocLib.Instance.GetDocReader(bytes, new PageDimensions(1080, 1920));
        var builder = new System.Text.StringBuilder();

        for (var page = 0; page < document.GetPageCount(); page++)
        {
            using var pageReader = document.GetPageReader(page);
            builder.AppendLine(pageReader.GetText());
        }

        return builder.ToString();
    }
}
