using MemoMind.Core.Models;

namespace MemoMind.Core.Interfaces;

public interface ITaskService
{
    Task<IReadOnlyList<TaskItem>> GetAllAsync();
    Task AddAsync(TaskItem taskItem);
    Task UpdateAsync(TaskItem taskItem);
    Task DeleteAsync(int id);
    void ClearChangeTracker();
    Task ResetAndSeedAsync();
}
