using MemoMind.Core.Models;

namespace MemoMind.Core.Interfaces;

public interface IChatMessageService
{
    Task<IReadOnlyList<ChatMessageRecord>> GetAllAsync();
    Task AddAsync(ChatMessageRecord message);
    Task DeleteAllAsync();
}
