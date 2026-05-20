namespace MemoMind.Core.Models;

/// <summary>
/// AI Function Calling 的工具定义，描述一个可供 AI 调用的函数。
/// 发送给 AI API 的 tools 数组即由 ToolDefinition 列表构成。
/// </summary>
public class ToolDefinition
{
    /// <summary>固定为 "function"</summary>
    public string Type { get; set; } = "function";

    /// <summary>函数的具体定义信息</summary>
    public FunctionDefinition Function { get; set; } = new();
}

/// <summary>
/// 函数的名称、描述和参数 JSON Schema。
/// 参数结构需符合 OpenAI Function Calling 格式：{ type, properties, required }。
/// </summary>
public class FunctionDefinition
{
    /// <summary>函数唯一标识名，如 "create_task"、"care_plant"</summary>
    public string Name { get; set; } = "";

    /// <summary>函数用途描述，AI 据此判断何时调用</summary>
    public string Description { get; set; } = "";

    /// <summary>参数 JSON Schema 对象，包含 type/properties/required 字段</summary>
    public object Parameters { get; set; } = new { };
}

/// <summary>
/// AI 返回的单个工具调用请求。
/// 从 AI 响应的 tool_calls 数组中解析得到。
/// </summary>
public class ToolCall
{
    /// <summary>工具调用的唯一 ID，用于关联后续 tool 消息</summary>
    public string Id { get; set; } = "";

    /// <summary>要调用的函数名</summary>
    public string FunctionName { get; set; } = "";

    /// <summary>函数参数的 JSON 字符串</summary>
    public string FunctionArguments { get; set; } = "";
}

/// <summary>
/// Agent 模式的完整响应，包含 AI 的自然语言回复和工具执行结果。
/// SendAgentAsync 的返回值。
/// </summary>
public class AgentResponse
{
    /// <summary>AI 生成的最终自然语言回复</summary>
    public string Reply { get; set; } = "";

    /// <summary>工具执行结果列表（可能为空）</summary>
    public IReadOnlyList<AgentToolResult> ToolResults { get; set; } = Array.Empty<AgentToolResult>();
}

/// <summary>
/// 单个工具的执行结果。
/// 在聊天界面中以"系统"消息的形式展示给用户。
/// </summary>
public class AgentToolResult
{
    /// <summary>工具名称，如 "create_task"、"care_plant"</summary>
    public string ToolName { get; set; } = "";

    /// <summary>工具执行结果描述，如 "任务「xxx」已创建成功"</summary>
    public string Message { get; set; } = "";

    /// <summary>工具是否执行成功</summary>
    public bool Success { get; set; } = true;
}
