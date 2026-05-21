using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using MemoMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MemoMind.Infrastructure.Services;

/// <summary>
/// 聊天消息的 EF Core 持久化实现，使用 SQLite 存储聊天记录。
///
/// 消息按 Time 升序排列，确保恢复后按原始顺序显示。
/// 仅支持全量清空（DeleteAllAsync），不提供单条删除——聊天记录保持完整历史。
/// </summary>
public class ChatMessageService : IChatMessageService
{
    private readonly AppDbContext dbContext;

    public ChatMessageService(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <summary>获取所有聊天消息，按发送时间升序排列</summary>
    public async Task<IReadOnlyList<ChatMessageRecord>> GetAllAsync()
    {
        return await dbContext.ChatMessages
            .OrderBy(m => m.Time)
            .ToListAsync();
    }

    /// <summary>新增一条聊天消息并立即保存到数据库</summary>
    public async Task AddAsync(ChatMessageRecord message)
    {
        dbContext.ChatMessages.Add(message);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>清空所有聊天记录（全量删除）</summary>
    public async Task DeleteAllAsync()
    {
        var all = await dbContext.ChatMessages.ToListAsync();
        dbContext.ChatMessages.RemoveRange(all);
        await dbContext.SaveChangesAsync();
    }
}
