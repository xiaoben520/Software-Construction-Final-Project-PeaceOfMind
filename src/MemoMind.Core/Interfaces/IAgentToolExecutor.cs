namespace MemoMind.Core.Interfaces;

/// <summary>
/// AI Agent 工具执行器的接口。
/// ChatService 在收到 AI 的工具调用请求后，通过此接口将 functionName 路由到具体的本地操作。
/// </summary>
public interface IAgentToolExecutor
{
    /// <summary>
    /// 根据函数名和 JSON 参数执行对应的本地操作。
    /// </summary>
    /// <param name="functionName">AI 请求的函数名，如 "create_task"、"care_plant"</param>
    /// <param name="argumentsJson">函数参数的 JSON 字符串</param>
    /// <returns>操作结果的中文描述，会展示给用户</returns>
    Task<string> ExecuteToolAsync(string functionName, string argumentsJson);
}
