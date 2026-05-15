namespace MemoMind.Core.Interfaces;

public interface IAgentToolExecutor
{
    Task<string> ExecuteToolAsync(string functionName, string argumentsJson);
}
