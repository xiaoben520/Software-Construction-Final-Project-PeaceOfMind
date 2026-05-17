using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using MemoMind.App.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MemoMind.App.Services;

public class ChatService : IChatService
{
    private readonly IAppSettingsStore settingsStore;
    private readonly HttpClient httpClient;

    private static readonly string DefaultPersonaPrompt =
        "你是一个温和、会倾听、会整理事项的 AI 心灵伙伴。说话简洁、友好、有共情，优先帮用户把事情理清。";

    private static readonly string[] OfflineResponses =
    [
        "我听到了。先别急，我们可以一件一件来。",
        "听起来你最近有点累，要不要先休息五分钟？",
        "你已经做得很好了，慢慢来。",
        "这件事我帮你记下了，你可以先做最小的一步。",
        "有时候压力大会让人感觉什么都做不完，但你已经比昨天进步了。",
        "要不要试试先把这件事拆成两个小步骤？",
        "没关系，累了就先放一放，照顾好自己也很重要。",
        "你今天已经完成了不少事情呢，给自己点个赞吧。",
        "我在这里陪着你，有什么想说的都可以跟我说。",
        "先做一件事就好，不用一次做完所有。",
        "你已经很努力了，这种感觉我理解。",
        "如果觉得难，可以先从最简单的部分开始。",
        "记住，完成比完美更重要。",
        "今天的你也在发光，即使你自己没注意到。",
        "有什么我可以帮你整理的吗？任务、心情，都可以。",
        "你的感受是真实的，不需要否定自己。",
        "一步一步来，我会一直在这里。",
        "要不我们先列个清单？把脑子里的事都写下来会轻松很多。",
        "你已经跨过了很多你以为跨不过的坎，这次也一样。",
        "深呼吸，然后我们一起来看看接下来做什么。"
    ];

    private static readonly string ChatSystemPrompt =
        "你是一个温和的陪伴助手，名叫MemoMind。你的目标是先共情，再给出轻量建议。" +
        "不要进行诊断，不要进行说教，不要否定用户感受。" +
        "回复应自然、友好，使用鼓励式的语气。" +
        "如果用户表达了负面情绪，先承接情绪再表达理解，最后给出轻量建议。" +
        "如果用户提到了任务或计划，可以添加到其他模块对应的模块，同时温和地帮忙梳理。";

    private readonly IServiceScopeFactory scopeFactory;

    public ChatService(IAppSettingsStore settingsStore, IServiceScopeFactory scopeFactory)
    {
        this.settingsStore = settingsStore;
        this.scopeFactory = scopeFactory;
        httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<string> SendAsync(string inputText)
    {
        return await SendAsync(ChatSystemPrompt, inputText);
    }

    public async Task<string> SendAsync(string systemPrompt, string inputText)
    {
        var settings = await settingsStore.LoadAsync();

        if (!settings.EnableAi || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return GetOfflineResponse(inputText) + "\n\n（AI 功能不可用，请在设置中配置并启用 AI）";
        }

        try
        {
            return await CallAiAsync(settings, systemPrompt, inputText);
        }
        catch (Exception ex)
        {
            return GetOfflineResponse(inputText) + $"\n\n（AI 调用失败：{ex.Message}）";
        }
    }

    public async Task<string> SendAsync(string inputText, IReadOnlyList<string> memories)
    {
        var augmentedPrompt = BuildMemoryAugmentedPrompt(memories);
        return await SendAsync(augmentedPrompt, inputText);
    }

    public async Task<string> SendWithContextAsync(string inputText, IReadOnlyList<string> memories, IReadOnlyList<ChatHistoryItem> history)
    {
        var settings = await settingsStore.LoadAsync();

        if (!settings.EnableAi || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return GetOfflineResponse(inputText) + "\n\n（AI 功能不可用，请在设置中配置并启用 AI）";
        }

        try
        {
            var augmentedPrompt = BuildMemoryAugmentedPrompt(memories);
            return await CallAiWithHistoryAsync(settings, augmentedPrompt, inputText, history);
        }
        catch (Exception ex)
        {
            return GetOfflineResponse(inputText) + $"\n\n（AI 调用失败：{ex.Message}）";
        }
    }

    public async Task<AgentResponse> SendAgentAsync(string inputText, IReadOnlyList<string> memories, IReadOnlyList<ChatHistoryItem> history)
    {
        var settings = await settingsStore.LoadAsync();
        var result = new AgentResponse { Reply = "", ToolResults = Array.Empty<AgentToolResult>() };

        if (!settings.EnableAi || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            result.Reply = GetOfflineResponse(inputText) + "\n\n（AI 功能不可用，请在设置中配置并启用 AI）";
            return result;
        }

        // Try native function calling first
        try
        {
            var basePrompt = BuildMemoryAugmentedPrompt(memories);
            basePrompt += "\n\n## 任务管理工具\n" +
                          "你可以调用以下函数来真正操作任务（不是假装操作）：\n" +
                          "- create_task: 创建新任务（参数：title必填, description选填, start_date选填, due_date选填, estimated_hours选填, estimated_minutes选填, is_urgent选填）\n" +
                          "- list_tasks: 查看所有任务\n" +
                          "- update_task: 更新任务（参数：title必填用于查找, new_title选填, description选填, status选填, is_urgent选填, start_date选填, due_date选填, estimated_hours选填, estimated_minutes选填）\n" +
                          "- delete_task: 删除任务（参数：title必填用于查找）\n" +
                          "\n## 赛博植物工具\n" +
                          "你可以调用以下函数来照顾用户的植物伙伴：\n" +
                          "- care_plant: 给植物浇水/施肥/晒太阳（参数：action必填，可选值water/fertilize/sunbathe；plant_type选填，不填则照料当前植物）\n" +
                          "- check_plant_status: 查看植物当前状态（参数：plant_type选填）\n" +
                          "- switch_plant: 切换到另一株植物（参数：plant_type必填，可以是中文名如'仙人掌'或英文id如'cactus'）\n" +
                          "- list_plants: 列出所有可用植物\n" +
                          "\n【必须遵守】\n" +
                          "用户说「创建/添加/新建/记一下/帮我安排」任务时 → 你必须调用 create_task\n" +
                          "用户说「查看/列出/有哪些/任务列表」 → 你必须调用 list_tasks\n" +
                          "用户说「完成/标记/修改/更新/改成」任务时 → 你必须调用 update_task\n" +
                          "用户说「删除/移除/取消/去掉」任务时 → 你必须调用 delete_task\n" +
                          "用户说「浇水/施肥/晒太阳/照顾植物/浇花」 → 你必须调用 care_plant\n" +
                          "用户说「植物怎么样/植物状态/看看植物/还好吗」 → 你必须调用 check_plant_status\n" +
                          "用户说「切换植物/换一棵/换到/去XX那边」 → 你必须调用 switch_plant\n" +
                          "用户说「有哪些植物/植物列表/看看植物」 → 你必须调用 list_plants\n" +
                          "不要只用文字说「已经帮你做了」却不调用工具！调用工具后根据实际结果回复。" +
                          "如果用户只是在倾诉心情、聊天，则不需要调用工具，正常回复即可。";

            var tools = Infrastructure.Services.AgentToolExecutor.GetAvailableTools();
            var nativeResult = await CallAiWithToolsAsync(settings, basePrompt, inputText, history, tools);

            // If native tools actually executed (not just diagnostics), return
            var realTools = new[] { "create_task", "list_tasks", "update_task", "delete_task", "care_plant", "check_plant_status", "switch_plant", "list_plants" };
            if (nativeResult.ToolResults.Any(tr => realTools.Contains(tr.ToolName)))
                return nativeResult;

            // If no tools called but user asked for task ops, fall back to text agent
            if (IsTaskRelatedInput(inputText))
            {
                try { return await TryTextAgentAsync(settings, memories, inputText, history); }
                catch (Exception textEx)
                {
                    return new AgentResponse
                    {
                        Reply = $"文本指令模式也失败了：{textEx.Message}",
                        ToolResults = new[] { new AgentToolResult { ToolName = "diagnostic", Message = $"TryTextAgentAsync 异常: {textEx.Message}", Success = false } }
                    };
                }
            }

            return nativeResult;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Native function calling not supported — use text-based agent for task ops
            if (IsTaskRelatedInput(inputText))
            {
                try { return await TryTextAgentAsync(settings, memories, inputText, history); }
                catch (Exception textEx)
                {
                    result.Reply = GetOfflineResponse(inputText) + $"\n\n（AI 调用失败：{textEx.Message}）";
                    return result;
                }
            }
            // Non-task chat: regular fallback
            try
            {
                var augmentedPrompt = BuildMemoryAugmentedPrompt(memories);
                var reply = await CallAiWithHistoryAsync(settings, augmentedPrompt, inputText, history);
                return new AgentResponse { Reply = reply, ToolResults = Array.Empty<AgentToolResult>() };
            }
            catch (Exception fallbackEx)
            {
                result.Reply = GetOfflineResponse(inputText) + $"\n\n（AI 调用失败：{fallbackEx.Message}）";
                return result;
            }
        }
        catch (Exception ex)
        {
            result.Reply = GetOfflineResponse(inputText) + $"\n\n（AI 调用失败：{ex.Message}）";
            return result;
        }
    }

    private async Task<AgentResponse> TryTextAgentAsync(
        UserSettings settings,
        IReadOnlyList<string> memories,
        string userInput,
        IReadOnlyList<ChatHistoryItem> history)
    {
        var toolResults = new List<AgentToolResult>();
        toolResults.Add(new AgentToolResult
        {
            ToolName = "diagnostic",
            Message = "原生 Function Calling 不可用，切换到 JSON 指令模式。",
            Success = true
        });

        var basePrompt = BuildMemoryAugmentedPrompt(memories);
        basePrompt += "\n\n## 操作协议（必须严格遵守）\n" +
            "你需要用一个 JSON 对象来回复，格式如下：\n\n" +
            "{\n" +
            "  \"action\": \"create_task\",\n" +
            "  \"args\": {\"title\": \"用户说的任务标题\", \"due_date\": \"2026-06-01 15:00\", \"is_urgent\": false},\n" +
            "  \"reply\": \"你的自然语言回复\"\n" +
            "}\n\n" +
            "action 可选值：\n" +
            "  任务：create_task, list_tasks, update_task, delete_task\n" +
            "  植物：care_plant, check_plant_status, switch_plant, list_plants\n" +
            "  无操作：none\n" +
            "create_task args: title(必填), description(选填), start_date(选填，格式yyyy-MM-dd HH:mm), due_date(选填，格式yyyy-MM-dd HH:mm), estimated_hours(选填，整数), estimated_minutes(选填，整数), is_urgent(选填，true/false)\n" +
            "update_task args: title(必填，用于查找原任务), new_title(选填), description(选填), status(选填Todo/Doing/Done), is_urgent(选填，true/false), start_date(选填), due_date(选填), estimated_hours(选填), estimated_minutes(选填)\n" +
            "delete_task args: title(必填，用于查找要删除的任务)\n" +
            "list_tasks args: {}\n" +
            "care_plant args: action(必填，water/fertilize/sunbathe), plant_type(选填)\n" +
            "check_plant_status args: plant_type(选填)\n" +
            "switch_plant args: plant_type(必填，植物中文名或英文id)\n" +
            "list_plants args: {}\n" +
            "\n【关键规则】\n" +
            "1. 如果用户要求操作任务或照料植物，action 填对应的操作名，args 填从用户消息中提取的真实参数\n" +
            "2. 如果用户只是聊天/倾诉，action 填 \"none\"，args 填 {}\n" +
            "3. 所有参数必须是用户原话中提到的真实内容，绝不可以用占位符\n" +
            "4. reply 用自然友好的中文回复用户\n" +
            "5. 只输出纯 JSON，不要包含 markdown 代码块标记或其他任何文字";

        var rawReply = await CallAiJsonModeAsync(settings, basePrompt, userInput, history);

        toolResults.Add(new AgentToolResult
        {
            ToolName = "diagnostic",
            Message = $"AI 原始回复 (前200字): {Truncate(rawReply, 200)}",
            Success = true
        });

        var cleanedReply = rawReply;
        var jsonStr = rawReply.Trim();

        // Strip markdown code fences if present
        if (jsonStr.StartsWith("```"))
        {
            var start = jsonStr.IndexOf('{');
            var end = jsonStr.LastIndexOf('}');
            if (start >= 0 && end > start)
                jsonStr = jsonStr[start..(end + 1)];
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            var action = "none";
            if (root.TryGetProperty("action", out var actionProp))
                action = actionProp.GetString() ?? "none";

            if (root.TryGetProperty("reply", out var replyProp))
                cleanedReply = replyProp.GetString() ?? rawReply;

            if (action != "none" && action != "chat")
            {
                var argsJson = "{}";
                if (root.TryGetProperty("args", out var argsProp))
                    argsJson = argsProp.ToString();

                toolResults.Add(new AgentToolResult
                {
                    ToolName = "diagnostic",
                    Message = $"解析到指令: ACTION={action}, JSON={Truncate(argsJson, 150)}",
                    Success = true
                });

                using (var scope = scopeFactory.CreateScope())
                {
                    var executor = scope.ServiceProvider.GetRequiredService<IAgentToolExecutor>();
                    var execResult = await executor.ExecuteToolAsync(action, argsJson);
                    toolResults.Add(new AgentToolResult
                    {
                        ToolName = action,
                        Message = execResult,
                        Success = true
                    });
                }
            }
            else
            {
                toolResults.Add(new AgentToolResult
                {
                    ToolName = "diagnostic",
                    Message = $"AI 判断为普通聊天 (action={action})，不执行任务操作。",
                    Success = true
                });
            }
        }
        catch (JsonException)
        {
            toolResults.Add(new AgentToolResult
            {
                ToolName = "diagnostic",
                Message = "AI 回复不是有效 JSON，任务操作未执行。",
                Success = false
            });
        }

        return new AgentResponse { Reply = cleanedReply, ToolResults = toolResults };
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return "(空)";
        return text.Length <= maxLen ? text : text[..maxLen] + "...";
    }

    private async Task<AgentResponse> CallAiWithToolsAsync(
        UserSettings settings,
        string systemPrompt,
        string userInput,
        IReadOnlyList<ChatHistoryItem> history,
        IReadOnlyList<ToolDefinition> tools)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl).TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-chat" : settings.AiModel;
        // DeepSeek requires |tools suffix to enable function calling
        if (baseUrl.Contains("deepseek", StringComparison.OrdinalIgnoreCase) && !model.Contains('|'))
        {
            model += "|tools";
        }
        var finalSystemPrompt = BuildSystemPrompt(settings, systemPrompt);

        var messages = new List<object>
        {
            new { role = "system", content = finalSystemPrompt }
        };

        if (history is not null)
        {
            foreach (var item in history)
            {
                messages.Add(new { role = item.Role, content = item.Content });
            }
        }

        messages.Add(new { role = "user", content = userInput });

        var requestBody = new
        {
            model,
            messages,
            tools,
            tool_choice = "auto",
            max_tokens = 500,
            temperature = 0.2
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
        request.Content = content;

        var response = await httpClient.SendAsync(request);
        var statusCode = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            var diagMsg = $"API 返回 {statusCode}，模型 {model} 可能不支持 Function Calling。";
            if (IsTaskRelatedInput(userInput))
                return new AgentResponse
                {
                    Reply = "抱歉，当前模型不支持任务操作。请在设置中切换到支持 Function Calling 的模型。",
                    ToolResults = new[] { new AgentToolResult { ToolName = "diagnostic", Message = diagMsg, Success = false } }
                };
            // For non-task chat, fall through to regular chat silently
            try
            {
                var fallbackReply = await CallAiWithHistoryAsync(settings, systemPrompt, userInput, history);
                return new AgentResponse { Reply = fallbackReply, ToolResults = Array.Empty<AgentToolResult>() };
            }
            catch (Exception fex)
            {
                return new AgentResponse { Reply = $"API 错误 ({statusCode}): {fex.Message}", ToolResults = Array.Empty<AgentToolResult>() };
            }
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        var choice = doc.RootElement.GetProperty("choices")[0];
        var message = choice.GetProperty("message");

        // Check if AI called a tool
        if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
        {
            var toolResults = new List<AgentToolResult>();

            // Add assistant message with tool_calls
            messages.Add(new
            {
                role = "assistant",
                content = (string?)null,
                tool_calls = toolCalls
            });

            // Execute each tool call
            foreach (var tc in toolCalls.EnumerateArray())
            {
                var callId = tc.GetProperty("id").GetString() ?? "";
                var func = tc.GetProperty("function");
                var funcName = func.GetProperty("name").GetString() ?? "";
                var funcArgs = func.GetProperty("arguments").GetString() ?? "{}";

                string execResult;
                using (var scope = scopeFactory.CreateScope())
                {
                    var executor = scope.ServiceProvider.GetRequiredService<IAgentToolExecutor>();
                    execResult = await executor.ExecuteToolAsync(funcName, funcArgs);
                }

                toolResults.Add(new AgentToolResult
                {
                    ToolName = funcName,
                    Message = execResult,
                    Success = true
                });

                // Add tool result message
                messages.Add(new
                {
                    role = "tool",
                    tool_call_id = callId,
                    content = execResult
                });
            }

            // Send back to AI for final response
            var followUpBody = new
            {
                model,
                messages,
                max_tokens = 500,
                temperature = 0.8
            };

            var followUpJson = JsonSerializer.Serialize(followUpBody);
            var followUpContent = new StringContent(followUpJson, Encoding.UTF8, "application/json");

            var followUpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            followUpRequest.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
            followUpRequest.Content = followUpContent;

            var followUpResponse = await httpClient.SendAsync(followUpRequest);
            followUpResponse.EnsureSuccessStatusCode();

            var followUpBody2 = await followUpResponse.Content.ReadAsStringAsync();
            using var doc2 = JsonDocument.Parse(followUpBody2);
            var finalContent = doc2.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return new AgentResponse
            {
                Reply = finalContent?.Trim() ?? "操作已完成。",
                ToolResults = toolResults
            };
        }

        // No tool call - AI chose not to call any function
        var textContent = message.GetProperty("content").GetString();
        var diagnostics = new List<AgentToolResult>();
        diagnostics.Add(new AgentToolResult
        {
            ToolName = "diagnostic",
            Message = $"模型={model} 状态码={statusCode} tool_calls=无。AI 未调用任何工具函数。",
            Success = true
        });
        if (IsTaskRelatedInput(userInput))
        {
            diagnostics.Add(new AgentToolResult
            {
                ToolName = "system",
                Message = "AI 未调用任务工具，操作未实际执行。请确认当前模型支持 Function Calling。",
                Success = false
            });
        }
        return new AgentResponse
        {
            Reply = textContent?.Trim() ?? "我收到了，但需要一点时间想一想。",
            ToolResults = diagnostics
        };
    }

    public async Task<IReadOnlyList<ExtractedMemory>> ExtractMemoriesAsync(string userMessage, string aiReply)
    {
        var settings = await settingsStore.LoadAsync();

        if (!settings.EnableAi || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return Array.Empty<ExtractedMemory>();
        }

        try
        {
            return await CallExtractionApiAsync(settings, userMessage, aiReply);
        }
        catch (Exception ex)
        {
            // Memory extraction fails silently — the main reply was already shown
            System.Diagnostics.Debug.WriteLine($"Memory extraction failed: {ex.Message}");
            return Array.Empty<ExtractedMemory>();
        }
    }

    private async Task<string> CallAiAsync(UserSettings settings, string systemPrompt, string userInput)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl).TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-chat" : settings.AiModel;
        var finalSystemPrompt = BuildSystemPrompt(settings, systemPrompt);

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = finalSystemPrompt },
                new { role = "user", content = userInput }
            },
            max_tokens = 500,
            temperature = 0.8
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
        request.Content = content;

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        var message = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return message?.Trim() ?? "我收到了，但需要一点时间想一想。";
    }

    private async Task<string> CallAiWithHistoryAsync(UserSettings settings, string systemPrompt, string userInput, IReadOnlyList<ChatHistoryItem> history)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl).TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-chat" : settings.AiModel;
        var finalSystemPrompt = BuildSystemPrompt(settings, systemPrompt);

        var messages = new List<object>
        {
            new { role = "system", content = finalSystemPrompt }
        };

        if (history is not null)
        {
            foreach (var item in history)
            {
                messages.Add(new { role = item.Role, content = item.Content });
            }
        }

        messages.Add(new { role = "user", content = userInput });

        var requestBody = new
        {
            model,
            messages,
            max_tokens = 500,
            temperature = 0.8
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
        request.Content = content;

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        var message = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return message?.Trim() ?? "我收到了，但需要一点时间想一想。";
    }

    private async Task<string> CallAiJsonModeAsync(UserSettings settings, string systemPrompt, string userInput, IReadOnlyList<ChatHistoryItem> history)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl).TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-chat" : settings.AiModel;
        var finalSystemPrompt = BuildSystemPrompt(settings, systemPrompt);

        var messages = new List<object>
        {
            new { role = "system", content = finalSystemPrompt }
        };

        if (history is not null)
        {
            foreach (var item in history)
            {
                messages.Add(new { role = item.Role, content = item.Content });
            }
        }

        messages.Add(new { role = "user", content = userInput });

        var requestBody = new
        {
            model,
            messages,
            max_tokens = 500,
            temperature = 0.3,
            response_format = new { type = "json_object" }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
        request.Content = content;

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        var message = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return message?.Trim() ?? "我收到了，但需要一点时间想一想。";
    }

    private static string BuildSystemPrompt(UserSettings settings, string systemPrompt)
    {
        var basePrompt = string.IsNullOrWhiteSpace(systemPrompt) ? ChatSystemPrompt : systemPrompt.Trim();
        var persona = string.IsNullOrWhiteSpace(settings.AiPersona) ? DefaultPersonaPrompt : settings.AiPersona.Trim();

        return basePrompt +
               "\n\n用户自定义的人设要求：" + persona +
               "\n请在不违背模块设定和安全要求的前提下，尽量体现该人设。";
    }

    private static string GetOfflineResponse(string input)
    {
        var lower = input.ToLowerInvariant();

        if (lower.Contains("累") || lower.Contains("困") || lower.Contains("疲惫") || lower.Contains("没力气"))
            return "听起来你现在很疲惫。没关系，休息也是重要的事，先给自己一点时间喘口气。";

        if (lower.Contains("焦虑") || lower.Contains("紧张") || lower.Contains("担心") || lower.Contains("害怕"))
            return "焦虑的感觉我理解。它只是在提醒你在意这件事。我们试试把它拆成很小的一步，也许就没那么可怕了。";

        if (lower.Contains("难过") || lower.Contains("伤心") || lower.Contains("哭") || lower.Contains("低落"))
            return "我能感受到你的难过。这种情绪是正常的，不需要急着赶走它。我在这里陪着你。";

        if (lower.Contains("烦") || lower.Contains("烦躁") || lower.Contains("暴躁"))
            return "有时候就是会很烦，这很正常。要不要先做一件能让你平静下来的小事？比如喝杯水、听首歌。";

        if (lower.Contains("迷茫") || lower.Contains("不知道") || lower.Contains("困惑") || lower.Contains("怎么办"))
            return "迷茫的时候不需要立刻找到答案。我们可以先把能确定的一两件事写下来，慢慢就清晰了。";

        if (lower.Contains("作业") || lower.Contains("考试") || lower.Contains("ddl") || lower.Contains("截止"))
            return "任务看起来很多的时候，先挑最容易完成的一件开始。五分钟之后你就会感觉好多了。";

        if (lower.Contains("压力") || lower.Contains("忙") || lower.Contains("好多"))
            return "压力大的时候，先把所有事情写下来会比在脑子里转要好。要我帮你列一下吗？";

        if (lower.Contains("谢谢") || lower.Contains("感谢") || lower.Contains("感恩"))
            return "不客气，能帮到你是我的荣幸。你值得被温柔对待。";

        if (lower.Contains("你好") || lower.Contains("嗨") || lower.Contains("hello") || lower.Contains("hi"))
            return "你好呀！今天怎么样？有什么我可以帮你的吗？";

        if (lower.Contains("晚安") || lower.Contains("睡了") || lower.Contains("睡觉"))
            return "晚安，好好休息。今天你已经做得很好了，明天会是新的一天。";

        if (lower.Contains("早安") || lower.Contains("早上") || lower.Contains("起床"))
            return "早安！新的一天开始啦，不用给自己太大压力，做好一件小事就很棒。";

        if (lower.Contains("无聊") || lower.Contains("没意思"))
            return "无聊的时候也许可以试试做一件一直想做但没开始的小事？或者我们聊聊天也不错。";

        if (lower.Contains("开心") || lower.Contains("高兴") || lower.Contains("快乐") || lower.Contains("好耶"))
            return "真好！开心的时候要好好享受这一刻，你值得这份快乐。";

        if (lower.Contains("任务") || lower.Contains("计划") || lower.Contains("安排"))
            return "好的，我可以帮你梳理任务。试着告诉我具体要做哪些事，我们一起来整理。";

        var random = new Random();
        return OfflineResponses[random.Next(OfflineResponses.Length)];
    }

    private static bool IsTaskRelatedInput(string input)
    {
        var taskKeywords = new[] { "创建", "添加", "新建", "任务", "记一下", "帮我安排", "查看", "列出", "有哪些",
                                   "完成", "标记", "修改", "更新", "改成", "删除", "移除", "取消", "去掉",
                                   "create", "add", "task", "todo", "done", "delete", "remove" };
        var plantKeywords = new[] { "浇水", "施肥", "晒太阳", "照顾植物", "浇花", "浇一下",
                                    "植物", "仙人掌", "向日葵", "薄荷", "蕨类", "竹子",
                                    "切换", "换一棵", "plant", "care" };
        return taskKeywords.Any(k => input.Contains(k, StringComparison.OrdinalIgnoreCase))
            || plantKeywords.Any(k => input.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildMemoryAugmentedPrompt(IReadOnlyList<string> memories)
    {
        if (memories is null || memories.Count == 0)
        {
            return ChatSystemPrompt;
        }

        var sb = new StringBuilder(ChatSystemPrompt);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("## 关于用户的长期记忆");
        sb.AppendLine("以下是关于用户的一些已知信息，请在回复中自然地参考这些信息，但不要刻意逐条罗列：");
        foreach (var memory in memories)
        {
            sb.AppendLine($"- {memory}");
        }

        return sb.ToString();
    }

    private async Task<IReadOnlyList<ExtractedMemory>> CallExtractionApiAsync(UserSettings settings, string userMessage, string aiReply)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl).TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-chat" : settings.AiModel;

        var extractionPrompt =
            "请分析以下对话，提取关于用户的值得长期记住的信息（如喜好、兴趣、习惯、个人信息、重要计划等）。" +
            "以JSON数组格式返回，每个条目包含 content（记忆内容，用简洁的中文描述）和 category（分类：喜好/兴趣/习惯/个人/计划/其他）。" +
            "如果对话中没有值得记录的新信息，返回空数组 []。" +
            "注意：只返回有效的JSON数组，不要包含markdown代码块标记或任何其他文字。";

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = extractionPrompt },
                new { role = "user", content = $"用户消息：{userMessage}\n\nAI回复：{aiReply}" }
            },
            max_tokens = 300,
            temperature = 0.3
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
        request.Content = content;

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);
        var message = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(message))
        {
            return Array.Empty<ExtractedMemory>();
        }

        var trimmed = message.Trim();
        if (trimmed.StartsWith("```"))
        {
            var start = trimmed.IndexOf('[');
            var end = trimmed.LastIndexOf(']');
            if (start >= 0 && end > start)
            {
                trimmed = trimmed[start..(end + 1)];
            }
        }

        try
        {
            var extracted = JsonSerializer.Deserialize<List<ExtractedMemory>>(trimmed, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return extracted?.Where(m => !string.IsNullOrWhiteSpace(m.Content)).ToList()
                   ?? (IReadOnlyList<ExtractedMemory>)Array.Empty<ExtractedMemory>();
        }
        catch
        {
            return Array.Empty<ExtractedMemory>();
        }
    }
}
