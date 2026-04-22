using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MemoMind.Infrastructure.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var databasePath = Path.Combine(AppContext.BaseDirectory, "MemoMind.db");
        optionsBuilder.UseSqlite($"Data Source={databasePath}");
        return new AppDbContext(optionsBuilder.Options);
    }
}
