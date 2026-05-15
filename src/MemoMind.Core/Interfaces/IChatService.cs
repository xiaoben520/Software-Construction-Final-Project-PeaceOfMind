using MemoMind.Core.Models;

namespace MemoMind.Core.Interfaces;

public interface IChatService
{
    Task<string> SendAsync(string inputText);
    Task<string> SendAsync(string systemPrompt, string inputText);
    Task<string> SendAsync(string inputText, IReadOnlyList<string> memories);
    Task<string> SendWithContextAsync(string inputText, IReadOnlyList<string> memories, IReadOnlyList<ChatHistoryItem> history);
    Task<AgentResponse> SendAgentAsync(string inputText, IReadOnlyList<string> memories, IReadOnlyList<ChatHistoryItem> history);
    Task<IReadOnlyList<ExtractedMemory>> ExtractMemoriesAsync(string userMessage, string aiReply);
}
