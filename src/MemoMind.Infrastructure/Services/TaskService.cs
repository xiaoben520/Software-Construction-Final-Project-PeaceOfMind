using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using MemoMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MemoMind.Infrastructure.Services;

public class TaskService : ITaskService
{
    private readonly AppDbContext dbContext;

    public TaskService(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync()
    {
        return await dbContext.Tasks
            .OrderByDescending(task => task.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(TaskItem taskItem)
    {
        dbContext.Tasks.Add(taskItem);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(TaskItem taskItem)
    {
        dbContext.Tasks.Update(taskItem);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var taskItem = await dbContext.Tasks.FindAsync(id);
        if (taskItem is null)
        {
            return;
        }

        dbContext.Tasks.Remove(taskItem);
        await dbContext.SaveChangesAsync();
    }

    public void ClearChangeTracker()
    {
        dbContext.ChangeTracker.Clear();
    }

    public async Task ResetAndSeedAsync()
    {
        dbContext.Tasks.RemoveRange(dbContext.Tasks);
        await dbContext.SaveChangesAsync();

        dbContext.Tasks.AddRange(
            new TaskItem
            {
                Title = "计网作业",
                Description = "完成课程作业并整理提交材料",
                DueDate = DateTime.Today.AddDays(2),
                IsUrgent = true,
                Status = "Todo",
                SourceType = "Seed"
            },
            new TaskItem
            {
                Title = "小组讨论",
                Description = "准备项目分工与展示内容",
                DueDate = DateTime.Today.AddDays(1),
                IsUrgent = false,
                Status = "Doing",
                SourceType = "Seed"
            });

        await dbContext.SaveChangesAsync();
    }
}
