using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using MemoMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MemoMind.Infrastructure.Services;

public class MemoryService : IMemoryService
{
    private readonly AppDbContext dbContext;

    public MemoryService(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<MemoryItem>> GetAllAsync()
    {
        return await dbContext.Memories
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task AddOrUpdateAsync(string content, string category)
    {
        var existing = await dbContext.Memories
            .FirstOrDefaultAsync(m => m.Content == content);

        if (existing is not null)
        {
            existing.Category = category;
        }
        else
        {
            dbContext.Memories.Add(new MemoryItem
            {
                Content = content,
                Category = category,
                CreatedAt = DateTime.Now
            });
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var item = await dbContext.Memories.FindAsync(id);
        if (item is not null)
        {
            dbContext.Memories.Remove(item);
            await dbContext.SaveChangesAsync();
        }
    }
}
