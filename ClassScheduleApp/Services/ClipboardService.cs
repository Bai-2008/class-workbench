using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace ClassScheduleApp.Services;

/// <summary>
/// 基于 System.Windows.Clipboard 的剪贴板实现。
/// 剪贴板可能被其它进程占用而抛 COM 异常，这里统一吞掉并返回失败，避免影响界面。
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    /// <inheritdoc />
    public bool TrySetText(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (COMException ex)
        {
            Debug.WriteLine($"写入剪贴板失败：{ex.Message}");
            return false;
        }
        catch (ExternalException ex)
        {
            Debug.WriteLine($"写入剪贴板失败：{ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public string? TryGetText()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (COMException ex)
        {
            Debug.WriteLine($"读取剪贴板失败：{ex.Message}");
            return null;
        }
        catch (ExternalException ex)
        {
            Debug.WriteLine($"读取剪贴板失败：{ex.Message}");
            return null;
        }
    }
}
