using MemoMind.Core.Models;

namespace MemoMind.Core.Interfaces;

/// <summary>
/// AI 聊天服务的核心接口，封装所有与 AI API 的通信逻辑。
/// 提供从简单对话到带工具调用的 Agent 模式四个层级的方法。
/// </summary>
public interface IChatService
{
    /// <summary>
    /// 简单对话：使用默认系统提示词，无记忆无历史。
    /// </summary>
    Task<string> SendAsync(string inputText);

    /// <summary>
    /// 带自定义系统提示词的对话。
    /// </summary>
    Task<string> SendAsync(string systemPrompt, string inputText);

    /// <summary>
    /// 带长期记忆的对话：记忆被注入到系统提示词中，AI 回复时会自然参考。
    /// </summary>
    Task<string> SendAsync(string inputText, IReadOnlyList<string> memories);

    /// <summary>
    /// 带完整上下文的对话：包含长期记忆和近期对话历史。
    /// AI 可以据此保持多轮对话的连贯性。
    /// </summary>
    Task<string> SendWithContextAsync(string inputText, IReadOnlyList<string> memories, IReadOnlyList<ChatHistoryItem> history);

    /// <summary>
    /// Agent 模式：AI 可以调用本地工具（创建任务、照料植物、设置闹钟等）来执行实际操作。
    /// 优先使用原生 Function Calling，不支持时回退到 JSON 指令模式。
    /// </summary>
    Task<AgentResponse> SendAgentAsync(string inputText, IReadOnlyList<string> memories, IReadOnlyList<ChatHistoryItem> history);

    /// <summary>
    /// 从一轮对话中提取值得长期记住的用户信息（喜好、习惯、计划等）。
    /// 提取结果由调用方写入 MemoryService，失败时静默丢弃。
    /// </summary>
    Task<IReadOnlyList<ExtractedMemory>> ExtractMemoriesAsync(string userMessage, string aiReply);
}
