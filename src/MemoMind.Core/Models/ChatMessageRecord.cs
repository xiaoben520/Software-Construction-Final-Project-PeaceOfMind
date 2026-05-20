namespace MemoMind.Core.Models;

/// <summary>
/// 聊天消息的数据库持久化模型，映射到 SQLite 的 ChatMessages 表。
/// 用于在应用重启后恢复聊天记录。
/// </summary>
public class ChatMessageRecord
{
    /// <summary>自增主键</summary>
    public int Id { get; set; }

    /// <summary>发送者名称："我" / "MemoMind" / "系统"</summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>消息正文内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>消息发送时间</summary>
    public DateTime Time { get; set; } = DateTime.Now;

    /// <summary>是否为用户发送的消息（决定 UI 气泡靠左还是靠右）</summary>
    public bool IsUserMessage { get; set; }
}
