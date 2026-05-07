namespace MemoMind.Core.Interfaces;

public interface IChatService
{
    Task<string> SendAsync(string inputText);
    Task<string> SendAsync(string systemPrompt, string inputText);
}
