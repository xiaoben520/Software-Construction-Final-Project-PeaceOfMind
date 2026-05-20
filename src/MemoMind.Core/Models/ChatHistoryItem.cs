namespace MemoMind.Core.Models;

/// <summary>
/// AI 对话历史中的单条消息，仅包含 role 和 content 两个字段。
/// 用于构建发送给 AI API 的 messages 数组，比 ChatMessageRecord 更精简。
/// </summary>
public class ChatHistoryItem
{
    /// <summary>角色标识："user" 或 "assistant"</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>消息正文内容</summary>
    public string Content { get; set; } = string.Empty;
}
