namespace MemoMind.Core.Models;

public class ToolDefinition
{
    public string Type { get; set; } = "function";
    public FunctionDefinition Function { get; set; } = new();
}

public class FunctionDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public object Parameters { get; set; } = new { };
}

public class ToolCall
{
    public string Id { get; set; } = "";
    public string FunctionName { get; set; } = "";
    public string FunctionArguments { get; set; } = "";
}

public class AgentResponse
{
    public string Reply { get; set; } = "";
    public IReadOnlyList<AgentToolResult> ToolResults { get; set; } = Array.Empty<AgentToolResult>();
}

public class AgentToolResult
{
    public string ToolName { get; set; } = "";
    public string Message { get; set; } = "";
    public bool Success { get; set; } = true;
}
