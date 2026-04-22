using MemoMind.Core.Models;

namespace MemoMind.Core.Interfaces;

public interface IFileWorkspaceService
{
    Task<IReadOnlyList<FileWorkspace>> GetAllAsync();
    Task AddAsync(FileWorkspace workspace);
    Task UpdateAsync(FileWorkspace workspace);
    Task DeleteAsync(int id);
}
