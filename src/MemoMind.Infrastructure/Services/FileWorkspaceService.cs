using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using MemoMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MemoMind.Infrastructure.Services;

public class FileWorkspaceService : IFileWorkspaceService
{
    private readonly AppDbContext dbContext;

    public FileWorkspaceService(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<FileWorkspace>> GetAllAsync()
    {
        return await dbContext.FileWorkspaces
            .OrderByDescending(x => x.LastOpenedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
    }

    public async Task AddAsync(FileWorkspace workspace)
    {
        dbContext.FileWorkspaces.Add(workspace);
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(FileWorkspace workspace)
    {
        dbContext.FileWorkspaces.Update(workspace);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var workspace = await dbContext.FileWorkspaces.FindAsync(id);
        if (workspace is null)
        {
            return;
        }

        dbContext.FileWorkspaces.Remove(workspace);
        await dbContext.SaveChangesAsync();
    }
}
