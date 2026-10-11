using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClassScheduleApp.Repositories;

/// <summary>
/// 识别 EF Core 抛出的数据库错误类型，便于仓储层转换成给用户看的友好提示。
/// </summary>
internal static class DbErrorInspector
{
    /// <summary>SQLite 主错误码：SQLITE_CONSTRAINT（含 UNIQUE / NOT NULL / CHECK / FOREIGN KEY）。</summary>
    private const int SqliteConstraintErrorCode = 19;

    /// <summary>是否为唯一约束（UNIQUE）冲突：学号、课程编号、选课三元组重复都会走这里。</summary>
    public static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        var inner = exception.InnerException;

        if (inner is SqliteException sqliteException)
        {
            return sqliteException.SqliteErrorCode == SqliteConstraintErrorCode;
        }

        return inner?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>取最内层异常的可读信息，用于拼装失败文案。</summary>
    public static string Describe(DbUpdateException exception)
    {
        return exception.InnerException?.Message ?? exception.Message;
    }
}
