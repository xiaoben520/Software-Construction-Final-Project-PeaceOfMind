using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using MemoMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MemoMind.Infrastructure.Services;

public class ChatMessageService : IChatMessageService
{
    private readonly AppDbContext dbContext;

    public ChatMessageService(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ChatMessageRecord>> GetAllAsync()
    {
        return await dbContext.ChatMessages
            .OrderBy(m => m.Time)
            .ToListAsync();
    }

    public async Task AddAsync(ChatMessageRecord message)
    {
        dbContext.ChatMessages.Add(message);
        await dbContext.SaveChangesAsync();
    }

    public async Task DeleteAllAsync()
    {
        var all = await dbContext.ChatMessages.ToListAsync();
        dbContext.ChatMessages.RemoveRange(all);
        await dbContext.SaveChangesAsync();
    }
}
