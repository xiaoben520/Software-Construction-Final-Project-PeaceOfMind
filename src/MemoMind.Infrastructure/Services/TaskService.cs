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
}
