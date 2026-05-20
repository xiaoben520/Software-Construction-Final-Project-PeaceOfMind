using MemoMind.Core.Models;

namespace MemoMind.Core.Interfaces;

/// <summary>
/// 聊天消息的持久化服务接口。
/// 负责将聊天记录写入 SQLite 并在应用启动时恢复。
/// </summary>
public interface IChatMessageService
{
    /// <summary>获取所有聊天消息，按时间升序排列</summary>
    Task<IReadOnlyList<ChatMessageRecord>> GetAllAsync();

    /// <summary>新增一条聊天消息</summary>
    Task AddAsync(ChatMessageRecord message);

    /// <summary>清空所有聊天记录</summary>
    Task DeleteAllAsync();
}
