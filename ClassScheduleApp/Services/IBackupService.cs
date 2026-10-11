namespace ClassScheduleApp.Services;

/// <summary>
/// 数据备份服务：把数据库快照与设置文件打包到 data/backup/backup_时间戳/ 下，只保留最近若干份。
/// </summary>
public interface IBackupService
{
    /// <summary>恢复成功后触发：各数据页据此重新查询，避免继续显示替换前的数据。</summary>
    event EventHandler? Restored;

    /// <summary>备份保留份数（只保留时间戳最新的这些份，超出的自动删除）。</summary>
    int KeepCount { get; }

    /// <summary>备份根目录（与数据库同级的 data/backup）。</summary>
    string BackupRootPath { get; }

    /// <summary>
    /// 最近一次备份时，因目录/文件被其它进程占用而未能清理的旧备份数量（0 表示全部清理成功）。
    /// 清理旧备份是尽力而为：个别旧目录删不掉不影响本次备份的可用性，界面据此如实提示。
    /// </summary>
    int LastSkippedBackupCount { get; }

    /// <summary>
    /// 执行一次备份，返回备份目录的完整路径。
    /// 数据库用 SQLite 的 VACUUM INTO 生成一致快照（不直接复制活动库文件），设置文件直接复制。
    /// 失败时抛出异常，异常信息含失败阶段与完整内部异常，供界面展示。
    /// 等价于按手动标签备份（手动备份不参与自动清理）。
    /// </summary>
    string Backup();

    /// <summary>
    /// 按原因标签执行一次备份（manual / daily / pre-op / pre-restore），目录名形如
    /// <c>backup_yyyyMMdd_HHmmss_标签</c>，返回备份目录的完整路径。
    /// 自动标签（daily / pre-op / pre-restore）保留最近 <see cref="AutoKeepCount"/> 份，手动备份不清理。
    /// </summary>
    string Backup(string reason);

    /// <summary>自动备份保留份数（仅作用于 daily / pre-op / pre-restore 与旧式 backup_ 目录）。</summary>
    int AutoKeepCount { get; }

    /// <summary>
    /// 每日自动备份：当日已存在 daily 备份则跳过；否则执行一次。
    /// 返回本次是否真的新建了备份。失败不抛出（启动路径不应因备份失败而阻断）。
    /// </summary>
    bool EnsureDailyBackup();

    /// <summary>列出 data/backup 下的全部备份（目录备份与旧式扁平快照），按时间倒序。</summary>
    IReadOnlyList<BackupItem> GetBackups();

    /// <summary>删除一个备份（目录或旧式扁平快照）。</summary>
    Repositories.OperationResult DeleteBackup(BackupItem item);

    /// <summary>
    /// 从指定备份恢复：先做完整性校验（无法打开或完整性检查不通过则拒绝恢复，库保持不动），
    /// 再对当前状态自动备份（pre-restore 标签），随后关闭数据库连接、替换数据库与设置文件。
    /// </summary>
    Repositories.OperationResult Restore(BackupItem item);
}
