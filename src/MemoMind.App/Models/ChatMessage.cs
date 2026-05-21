namespace MemoMind.App.Models;

/// <summary>
/// 聊天消息的 UI 绑定模型，用于 WPF 的 ObservableCollection 数据绑定。
/// 与 ChatMessageRecord（数据库模型）字段一致但属于 App 层，不依赖数据库上下文。
/// IsUserMessage 决定气泡位置：true → 右对齐蓝色气泡，false → 左对齐灰色气泡。
/// </summary>
public class ChatMessage
{
    /// <summary>发送者名称："我" / "MemoMind" / "系统"</summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>消息正文内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>消息发送时间，显示在气泡右上角（HH:mm 格式）</summary>
    public DateTime Time { get; set; } = DateTime.Now;

    /// <summary>是否为用户消息，控制气泡对齐和颜色</summary>
    public bool IsUserMessage { get; set; }
}
