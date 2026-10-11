using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClassScheduleApp.Data;

/// <summary>
/// 设计时上下文工厂：供 dotnet ef 工具创建 AppDbContext。
/// WPF 工程没有 Host 通用主机，必须显式提供该入口；
/// 复用 DatabasePathProvider 以保证设计时与运行时指向同一个库文件。
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(DatabasePathProvider.GetConnectionString())
            .Options;

        return new AppDbContext(options);
    }
}
