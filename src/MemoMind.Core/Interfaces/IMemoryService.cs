using MemoMind.Core.Models;

namespace MemoMind.Core.Interfaces;

public interface IMemoryService
{
    Task<IReadOnlyList<MemoryItem>> GetAllAsync();
    Task AddOrUpdateAsync(string content, string category);
    Task DeleteAsync(int id);
}
