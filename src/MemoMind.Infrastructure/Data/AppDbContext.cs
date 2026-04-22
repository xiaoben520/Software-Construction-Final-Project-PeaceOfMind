using MemoMind.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace MemoMind.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<EmotionLog> EmotionLogs => Set<EmotionLog>();
    public DbSet<FileWorkspace> FileWorkspaces => Set<FileWorkspace>();
    public DbSet<PomodoroSession> PomodoroSessions => Set<PomodoroSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TaskItem>().HasKey(x => x.Id);
        modelBuilder.Entity<CalendarEvent>().HasKey(x => x.Id);
        modelBuilder.Entity<EmotionLog>().HasKey(x => x.Id);
        modelBuilder.Entity<FileWorkspace>().HasKey(x => x.Id);
        modelBuilder.Entity<PomodoroSession>().HasKey(x => x.Id);
    }
}
