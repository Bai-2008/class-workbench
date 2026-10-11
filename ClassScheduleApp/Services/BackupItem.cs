using System.Globalization;
using System.IO;

namespace ClassScheduleApp.Services;

/// <summary>
/// 备份列表项：一行对应 data/backup 下的一个备份（目录备份或旧式扁平 .db 快照）。
/// </summary>
public sealed class BackupItem
{
    /// <summary>备份的完整路径（目录或 .db 文件）。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>备份名称（目录名或文件名）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>备份时间（取目录/文件时间或名称里的时间戳）。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>占用大小（字节）。</summary>
    public long SizeInBytes { get; set; }

    /// <summary>原因标签：manual / daily / pre-op / pre-restore；旧式扁平快照为 legacy。</summary>
    public string Reason { get; set; } = BackupReasons.Legacy;

    /// <summary>是否为目录备份（旧式扁平 .db 快照为 false）。</summary>
    public bool IsDirectory { get; set; }

    /// <summary>时间展示文本。</summary>
    public string CreatedAtText => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>大小展示文本。</summary>
    public string SizeText => SizeInBytes >= 1024 * 1024
        ? (SizeInBytes / 1024d / 1024d).ToString("0.##", CultureInfo.CurrentCulture) + " MB"
        : (SizeInBytes / 1024d).ToString("0.#", CultureInfo.CurrentCulture) + " KB";

    /// <summary>原因展示文本。</summary>
    public string ReasonText => BackupReasons.Describe(Reason);

    /// <summary>是否手动备份（手动备份不参与自动清理）。</summary>
    public bool IsManual => string.Equals(Reason, BackupReasons.Manual, StringComparison.Ordinal);
}
