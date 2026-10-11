using System.IO;

namespace ClassScheduleApp.Services;

/// <summary>
/// 备份原因标签：命名里带标签便于区分"谁触发的备份"，并决定是否参与自动清理。
/// </summary>
public static class BackupReasons
{
    /// <summary>手动备份（「立即备份数据」）：不参与自动清理。</summary>
    public const string Manual = "manual";

    /// <summary>每日自动备份：当日首次启动触发一次。</summary>
    public const string Daily = "daily";

    /// <summary>操作前备份（导入 / 批量删除 / 清空课表等破坏性操作前）。</summary>
    public const string PreOp = "pre-op";

    /// <summary>恢复前备份（替换数据库前对当前状态做的一次保护性备份）。</summary>
    public const string PreRestore = "pre-restore";

    /// <summary>旧式扁平快照（本功能之前的 &lt;名称&gt;.db 文件），按手动看待、不自动清理。</summary>
    public const string Legacy = "legacy";

    /// <summary>参与自动清理的标签（保留最近 AutoKeepCount 份，超出清最旧）。</summary>
    public static bool IsAutoPruned(string reason)
        => string.Equals(reason, Daily, StringComparison.Ordinal)
            || string.Equals(reason, PreOp, StringComparison.Ordinal)
            || string.Equals(reason, PreRestore, StringComparison.Ordinal)
            || string.Equals(reason, Legacy, StringComparison.Ordinal);

    /// <summary>标签的中文说明。</summary>
    public static string Describe(string reason) => reason switch
    {
        Manual => "手动备份",
        Daily => "每日自动",
        PreOp => "操作前自动",
        PreRestore => "恢复前自动",
        Legacy => "历史快照（手动）",
        _ => reason,
    };

    /// <summary>从备份目录/文件名解析原因标签；解析不出按 legacy。</summary>
    public static string ParseFromName(string name)
    {
        foreach (var reason in new[] { PreRestore, PreOp, Manual, Daily })
        {
            if (name.Contains("_" + reason, StringComparison.OrdinalIgnoreCase))
            {
                return reason;
            }
        }

        // 旧式扁平快照（manual_时间戳_原因.db / before_*.db）按手动看待，不自动清理
        if (!name.StartsWith("backup_", StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
        {
            return Legacy;
        }

        // 本功能之前的 backup_时间戳 目录：按每日自动看待，参与清理
        return Daily;
    }
}
