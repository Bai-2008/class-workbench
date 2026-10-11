using System.IO;
using Microsoft.Data.Sqlite;

namespace ClassScheduleApp.Data;

/// <summary>
/// 统一解析 SQLite 数据库文件位置：始终指向工作区根目录下的 data/classschedule.db。
/// 目的：避免 dotnet ef（设计时进程）与应用运行时因“当前工作目录”不同而连到两个不同的库文件。
/// 语义上仍是相对路径 data/classschedule.db，并把目录自动创建好。
/// 注意：不能用 ClassScheduleApp.csproj 作为上溯标志——那会把 data/ 解析到项目内的 Data 层源码目录，
/// 而 Windows 路径大小写不敏感，data 与 Data 会变成同一个文件夹。
/// </summary>
public static class DatabasePathProvider
{
    /// <summary>数据库文件相对根的路径。</summary>
    public const string RelativeDatabasePath = "data/classschedule.db";

    /// <summary>优先使用的上溯标志文件：解决方案文件位于工作区根目录。</summary>
    private const string SolutionMarkerFile = "ClassScheduleApp.sln";

    /// <summary>兜底的上溯标志文件。</summary>
    private const string ProjectMarkerFile = "ClassScheduleApp.csproj";

    /// <summary>取得数据库文件的绝对路径，并确保所在目录已存在。</summary>
    public static string GetDatabaseFilePath()
    {
        var root = FindRoot() ?? AppContext.BaseDirectory;
        var filePath = Path.Combine(root, RelativeDatabasePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        return filePath;
    }

    /// <summary>取得 SQLite 连接字符串。</summary>
    public static string GetConnectionString()
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = GetDatabaseFilePath(),
        }.ToString();
    }

    /// <summary>从程序集所在目录逐级上溯，按标志文件优先级定位根目录。</summary>
    private static string? FindRoot()
    {
        foreach (var marker in new[] { SolutionMarkerFile, ProjectMarkerFile })
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, marker)))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}
