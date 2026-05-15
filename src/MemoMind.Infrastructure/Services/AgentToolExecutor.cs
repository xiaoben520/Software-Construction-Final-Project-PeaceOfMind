using System.Text.Json;
using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;

namespace MemoMind.Infrastructure.Services;

public class AgentToolExecutor : IAgentToolExecutor
{
    private readonly ITaskService taskService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AgentToolExecutor(ITaskService taskService)
    {
        this.taskService = taskService;
    }

    public async Task<string> ExecuteToolAsync(string functionName, string argumentsJson)
    {
        return functionName switch
        {
            "create_task" => await CreateTaskAsync(argumentsJson),
            "list_tasks" => await ListTasksAsync(),
            "update_task" => await UpdateTaskAsync(argumentsJson),
            "delete_task" => await DeleteTaskAsync(argumentsJson),
            _ => "未知操作：{functionName}"
        };
    }

    private async Task<string> CreateTaskAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            var title = GetStringProperty(root, "title") ?? "未命名任务";
            var description = GetStringProperty(root, "description") ?? "";
            var isUrgent = GetBoolProperty(root, "is_urgent");

            DateTime? dueDate = null;
            if (root.TryGetProperty("due_date", out var dd) && dd.ValueKind == JsonValueKind.String)
            {
                var ds = dd.GetString();
                if (!string.IsNullOrWhiteSpace(ds))
                {
                    if (DateTime.TryParse(ds, out var parsed))
                    {
                        dueDate = parsed;
                    }
                }
            }

            var task = new TaskItem
            {
                Title = title,
                Description = description,
                IsUrgent = isUrgent,
                DueDate = dueDate,
                Status = "Todo",
                CreatedAt = DateTime.Now,
                SourceType = "Agent"
            };

            await taskService.AddAsync(task);

            var dueInfo = dueDate.HasValue ? $"，截止时间：{dueDate:yyyy-MM-dd HH:mm}" : "";
            var urgentInfo = isUrgent ? "（紧急）" : "";
            return $"任务「{title}」已创建成功{urgentInfo}{dueInfo}。";
        }
        catch (Exception ex)
        {
            return $"创建任务失败：{ex.Message}";
        }
    }

    private async Task<string> ListTasksAsync()
    {
        try
        {
            var tasks = await taskService.GetAllAsync();
            if (tasks.Count == 0)
            {
                return "当前没有任何任务。";
            }

            var lines = new List<string> { $"当前共有 {tasks.Count} 个任务：" };
            foreach (var t in tasks)
            {
                var status = t.Status switch
                {
                    "Todo" => "待办",
                    "Doing" => "进行中",
                    "Done" => "已完成",
                    _ => t.Status
                };
                var due = t.DueDate.HasValue ? $" 截止：{t.DueDate:MM-dd HH:mm}" : "";
                var urgent = t.IsUrgent ? " ⚠紧急" : "";
                lines.Add($"  [{status}] {t.Title}{due}{urgent}");
            }

            return string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            return $"获取任务列表失败：{ex.Message}";
        }
    }

    private async Task<string> UpdateTaskAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            var title = GetStringProperty(root, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                return "请指定要更新的任务标题。";
            }

            var tasks = await taskService.GetAllAsync();
            var target = tasks.FirstOrDefault(t =>
                t.Title.Contains(title, StringComparison.OrdinalIgnoreCase) ||
                title.Contains(t.Title, StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                return $"未找到标题包含「{title}」的任务。请先用 list_tasks 查看所有任务。";
            }

            var newTitle = GetStringProperty(root, "new_title");
            var newStatus = GetStringProperty(root, "status");
            var isUrgentStr = GetStringProperty(root, "is_urgent");

            var changes = new List<string>();

            if (!string.IsNullOrWhiteSpace(newTitle) && newTitle != target.Title)
            {
                target.Title = newTitle;
                changes.Add($"标题→「{newTitle}」");
            }

            if (!string.IsNullOrWhiteSpace(newStatus))
            {
                var normalized = newStatus switch
                {
                    "todo" or "待办" => "Todo",
                    "doing" or "进行中" => "Doing",
                    "done" or "已完成" => "Done",
                    _ => null
                };
                if (normalized is not null && normalized != target.Status)
                {
                    target.Status = normalized;
                    if (normalized == "Done")
                    {
                        target.CompletedAt = DateTime.Now;
                    }
                    changes.Add($"状态→{newStatus}");
                }
            }

            if (!string.IsNullOrWhiteSpace(isUrgentStr) && bool.TryParse(isUrgentStr, out var urgent))
            {
                target.IsUrgent = urgent;
                changes.Add(urgent ? "标记为紧急" : "取消紧急标记");
            }

            if (changes.Count == 0)
            {
                return $"任务「{target.Title}」没有需要更新的内容。";
            }

            await taskService.UpdateAsync(target);
            return $"任务「{target.Title}」已更新：{string.Join("，", changes)}。";
        }
        catch (Exception ex)
        {
            return $"更新任务失败：{ex.Message}";
        }
    }

    private async Task<string> DeleteTaskAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            var title = GetStringProperty(root, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                return "请指定要删除的任务标题。";
            }

            var tasks = await taskService.GetAllAsync();
            var target = tasks.FirstOrDefault(t =>
                t.Title.Contains(title, StringComparison.OrdinalIgnoreCase) ||
                title.Contains(t.Title, StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                return $"未找到标题包含「{title}」的任务。";
            }

            await taskService.DeleteAsync(target.Id);
            return $"任务「{target.Title}」已删除。";
        }
        catch (Exception ex)
        {
            return $"删除任务失败：{ex.Message}";
        }
    }

    public static IReadOnlyList<ToolDefinition> GetAvailableTools()
    {
        return new[]
        {
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "create_task",
                    Description = "创建一个新任务。当用户说'帮我创建任务'、'添加任务'、'新建任务'、'记一下'等时调用。需要从用户的话中提取任务的标题、描述、截止日期等信息。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new { type = "string", description = "任务标题" },
                            description = new { type = "string", description = "任务描述，如果用户没有提供则留空" },
                            due_date = new { type = "string", description = "截止日期，格式 yyyy-MM-dd HH:mm，如果用户没有提供则不传" },
                            is_urgent = new { type = "boolean", description = "是否紧急，默认false" }
                        },
                        required = new[] { "title" }
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "list_tasks",
                    Description = "查看所有任务的列表。当用户说'查看任务'、'有哪些任务'、'任务列表'、'所有任务'等时调用。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new { },
                        required = Array.Empty<string>()
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "update_task",
                    Description = "更新一个已有任务的状态或内容。当用户说'完成任务'、'标记为已完成'、'修改任务'、'更新任务'、'把XX改成'等时调用。需要提供原任务标题来找到任务。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new { type = "string", description = "要更新的原任务标题（或标题关键词）" },
                            new_title = new { type = "string", description = "新标题，如果不改则不传" },
                            status = new { type = "string", description = "新状态：todo/待办, doing/进行中, done/已完成" },
                            is_urgent = new { type = "string", description = "是否紧急：true/false" }
                        },
                        required = new[] { "title" }
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "delete_task",
                    Description = "删除一个任务。当用户说'删除任务'、'移除任务'、'取消任务'等时调用。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new { type = "string", description = "要删除的任务标题（或标题关键词）" }
                        },
                        required = new[] { "title" }
                    }
                }
            }
        };
    }

    private static string? GetStringProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    private static bool GetBoolProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return prop.GetBoolean();
        }
        return false;
    }
}
