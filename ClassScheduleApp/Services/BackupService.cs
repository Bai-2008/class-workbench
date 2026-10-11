using System.IO;
using ClassScheduleApp.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Services;

/// <summary>
/// 数据备份实现。
/// 数据库快照走 SQLite 的 <c>VACUUM INTO</c>（经 IDbContextFactory 的短生命周期 context 执行），
/// 而不是复制活动库文件——避免备到正在写入中的库；目标路径用 SQL 参数传入，不存在引号转义问题。
/// </summary>
public sealed class BackupService : IBackupService
{
    private const int AutoKeepCountValue = 20;
    private const string DatabaseFileName = "classschedule.db";
    private const string SettingsFileName = "settings.json";

    private readonly IDbContextFactory<AppDbContext> _factory;

    /// <inheritdoc />
    public event EventHandler? Restored;

    public BackupService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public int AutoKeepCount => AutoKeepCountValue;

    /// <inheritdoc />
    public int KeepCount => AutoKeepCountValue;

    /// <inheritdoc />
    public int LastSkippedBackupCount { get; private set; }

    /// <inheritdoc />
    public string BackupRootPath
    {
        get
        {
            var dataDirectory = Path.GetDirectoryName(DatabasePathProvider.GetDatabaseFilePath()) ?? AppContext.BaseDirectory;
            return Path.Combine(dataDirectory, "backup");
        }
    }

    /// <inheritdoc />
    public string Backup() => Backup(BackupReasons.Manual);

    /// <inheritdoc />
    public string Backup(string reason)
    {
        var backupRoot = BackupRootPath;
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var label = string.IsNullOrWhiteSpace(reason) ? BackupReasons.Manual : reason;
        var targetDirectory = Path.Combine(backupRoot, $"backup_{timestamp}_{label}");

        Directory.CreateDirectory(backupRoot);
        targetDirectory = EnsureUniqueDirectory(targetDirectory);

        var databaseSnapshotDone = false;
        var phase = "准备备份目录";
        try
        {
            Directory.CreateDirectory(targetDirectory);

            phase = "生成数据库快照";
            BackupDatabase(Path.Combine(targetDirectory, DatabaseFileName));
            databaseSnapshotDone = true;

            phase = "复制设置文件";
            BackupSettings(Path.Combine(targetDirectory, SettingsFileName));

            phase = "清理过期备份";
            PruneOldBackups(backupRoot);

            return targetDirectory;
        }
        catch (Exception ex)
        {
            // 数据库快照还没成功时，清掉半成品目录；已经成功则保留（备份可用，失败发生在后续阶段）
            if (!databaseSnapshotDone)
            {
                TryDeleteDirectory(targetDirectory);
            }

            throw new InvalidOperationException(
                $"备份失败（阶段：{phase}，目标目录：{targetDirectory}）：{ex.Message}", ex);
        }
    }

    /// <summary>同一秒内多次备份时避免目录重名（补零后缀，保证名称排序仍是时间顺序）。</summary>
    private static string EnsureUniqueDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return directory;
        }

        for (var index = 2; index < 1000; index++)
        {
            var candidate = $"{directory}_{index:D2}";
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"备份目录重名过多，无法创建：{directory}");
    }

    /// <summary>用 VACUUM INTO 生成一致快照；目标路径以参数形式传入 SQL。</summary>
    private void BackupDatabase(string targetFilePath)
    {
        var absolutePath = Path.GetFullPath(targetFilePath);

        using var ctx = _factory.CreateDbContext();
        ctx.Database.ExecuteSqlRaw("VACUUM INTO {0}", absolutePath);
    }

    /// <summary>复制 settings.json；尚未生成时写一个空对象，保证备份目录始终包含这两个文件。</summary>
    private void BackupSettings(string targetFilePath)
    {
        var dataDirectory = Path.GetDirectoryName(DatabasePathProvider.GetDatabaseFilePath()) ?? AppContext.BaseDirectory;
        var sourcePath = Path.Combine(dataDirectory, SettingsFileName);

        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, targetFilePath, overwrite: true);
            return;
        }

        File.WriteAllText(targetFilePath, "{ }" + Environment.NewLine);
    }

    /// <summary>
    /// 自动清理：只统计按名称排序（时间戳前缀保证顺序）后可清理的条目——
    /// 目录备份与旧式 backup_ 目录按标签决定是否参与，手动备份与旧式扁平快照一律不动。
    /// 超过 AutoKeepCount 份时从最旧开始删；尽力而为，个别删除失败只计数。
    /// </summary>
    private void PruneOldBackups(string backupRoot)
    {
        var entries = new List<BackupItem>();
        foreach (var item in GetBackups())
        {
            if (item.IsDirectory && BackupReasons.IsAutoPruned(item.Reason))
            {
                entries.Add(item);
            }
        }

        entries.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));

        LastSkippedBackupCount = 0;
        var removeCount = entries.Count - AutoKeepCountValue;
        for (var index = 0; index < removeCount; index++)
        {
            try
            {
                Directory.Delete(entries[index].Path, recursive: true);
            }
            catch (IOException)
            {
                LastSkippedBackupCount++;
            }
            catch (UnauthorizedAccessException)
            {
                LastSkippedBackupCount++;
            }
        }
    }

    /// <inheritdoc />
    public bool EnsureDailyBackup()
    {
        try
        {
            var today = DateTime.Today;
            foreach (var item in GetBackups())
            {
                if (string.Equals(item.Reason, BackupReasons.Daily, StringComparison.Ordinal) &&
                    item.CreatedAt.Date == today)
                {
                    return false;
                }
            }

            Backup(BackupReasons.Daily);
            return true;
        }
        catch (Exception)
        {
            // 启动路径上的每日备份失败不应阻断应用启动
            return false;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<BackupItem> GetBackups()
    {
        var root = BackupRootPath;
        var items = new List<BackupItem>();
        if (!Directory.Exists(root))
        {
            return items;
        }

        foreach (var directory in Directory.GetDirectories(root, "backup_*"))
        {
            var name = Path.GetFileName(directory);
            items.Add(new BackupItem
            {
                Path = directory,
                Name = name,
                CreatedAt = ResolveCreatedAt(name, directory),
                SizeInBytes = DirectorySize(directory),
                Reason = BackupReasons.ParseFromName(name),
                IsDirectory = true,
            });
        }

        // 旧式扁平快照（本功能之前的 manual_*/before_*.db）：列出来、可删除与恢复，但不参与自动清理
        foreach (var file in Directory.GetFiles(root, "*.db"))
        {
            var name = Path.GetFileName(file);
            items.Add(new BackupItem
            {
                Path = file,
                Name = name,
                CreatedAt = File.GetLastWriteTime(file),
                SizeInBytes = new FileInfo(file).Length,
                Reason = BackupReasons.Legacy,
                IsDirectory = false,
            });
        }

        items.Sort(static (left, right) => right.CreatedAt.CompareTo(left.CreatedAt));
        return items;
    }

    /// <inheritdoc />
    public Repositories.OperationResult DeleteBackup(BackupItem item)
    {
        try
        {
            if (item.IsDirectory)
            {
                if (Directory.Exists(item.Path))
                {
                    Directory.Delete(item.Path, recursive: true);
                }
            }
            else if (File.Exists(item.Path))
            {
                File.Delete(item.Path);
            }

            return Repositories.OperationResult.Ok();
        }
        catch (Exception ex)
        {
            return Repositories.OperationResult.Fail($"删除备份失败：{ex.Message}");
        }
    }

    /// <inheritdoc />
    public Repositories.OperationResult Restore(BackupItem item)
    {
        var sourceDatabase = item.IsDirectory
            ? Path.Combine(item.Path, DatabaseFileName)
            : item.Path;

        // 1) 完整性校验：打不开或完整性检查不通过 → 拒绝恢复，库保持不动
        var problem = VerifyDatabaseFile(sourceDatabase);
        if (problem is not null)
        {
            return Repositories.OperationResult.Fail($"该备份不可用于恢复：{problem}（当前数据未做任何改动）");
        }

        // 2) 恢复前自动备份当前状态（pre-restore）
        string preRestorePath;
        try
        {
            preRestorePath = Backup(BackupReasons.PreRestore);
        }
        catch (Exception ex)
        {
            return Repositories.OperationResult.Fail($"恢复前自动备份失败，已中止恢复：{ex.Message}");
        }

        try
        {
            var liveDatabase = DatabasePathProvider.GetDatabaseFilePath();

            // 3) 关闭所有连接：清空连接池并回收仍存活的 DbContext
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // 4) 替换数据库，并清掉旧库的日志残留
            File.Copy(sourceDatabase, liveDatabase, overwrite: true);
            foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
            {
                var residual = liveDatabase + suffix;
                if (File.Exists(residual))
                {
                    File.Delete(residual);
                }
            }

            // 5) 目录备份同时带回设置文件
            if (item.IsDirectory)
            {
                var sourceSettings = Path.Combine(item.Path, SettingsFileName);
                if (File.Exists(sourceSettings))
                {
                    var liveSettings = Path.Combine(Path.GetDirectoryName(liveDatabase) ?? AppContext.BaseDirectory, SettingsFileName);
                    File.Copy(sourceSettings, liveSettings, overwrite: true);
                }
            }

            // 6) 通知各数据页重新查询（替换后的数据已就绪）
            Restored?.Invoke(this, EventArgs.Empty);

            return Repositories.OperationResult.Ok();
        }
        catch (Exception ex)
        {
            return Repositories.OperationResult.Fail(
                $"恢复失败（阶段：替换数据文件；恢复前备份已保存在 {preRestorePath}）：{ex.Message}");
        }
    }

    /// <summary>返回 null 表示可用；否则返回不可用的原因。</summary>
    private static string? VerifyDatabaseFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return "备份文件不存在";
        }

        if (new FileInfo(filePath).Length == 0)
        {
            return "备份文件为空";
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = filePath,
                Mode = SqliteOpenMode.ReadOnly,
            };

            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA integrity_check;";
                var result = command.ExecuteScalar() as string;
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    return $"数据库完整性检查未通过（{result}）";
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Students';";
                if (Convert.ToInt64(command.ExecuteScalar()) == 0)
                {
                    return "备份内容不是本应用的数据库（缺少 Students 表）";
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            return "无法打开备份数据库：" + ex.Message;
        }
    }

    /// <summary>从名称里的时间戳解析时间，解析不出退回目录/文件时间。</summary>
    private static DateTime ResolveCreatedAt(string name, string path)
    {
        var parts = name.Split('_');
        if (parts.Length >= 3 &&
            DateTime.TryParseExact($"{parts[1]}_{parts[2]}", "yyyyMMdd_HHmmss", null,
                System.Globalization.DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return Directory.Exists(path) ? Directory.GetLastWriteTime(path) : File.GetLastWriteTime(path);
    }

    private static long DirectorySize(string directory)
    {
        long total = 0;
        try
        {
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                total += new FileInfo(file).Length;
            }
        }
        catch (IOException)
        {
            // 统计失败不影响列表展示
        }

        return total;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // 清理半成品失败不掩盖原始错误
        }
        catch (UnauthorizedAccessException)
        {
            // 同上
        }
    }
}
