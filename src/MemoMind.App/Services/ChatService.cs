using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using MemoMind.App.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MemoMind.App.Services;

/// <summary>
/// AI 聊天服务的核心实现。
///
/// 职责：
/// 1. 封装所有与 AI API（OpenAI 兼容格式）的通信
/// 2. AI 不可用时提供离线关键词 + 随机温暖回复
/// 3. 实现 Agent 模式：AI 可调用本地工具（任务、植物、计时器）
/// 4. 自动从对话中提取长期记忆
///
/// 三种回复策略：
/// - 离线模式：关键词匹配 + 随机回复（AI 未启用或 API 调用失败时）
/// - 普通聊天：标准 Chat Completions API
/// - Agent 模式：Function Calling → 工具执行 → 结果反馈 → AI 最终回复
///   原生 Function Calling 不可用时，回退到 JSON 指令模式
/// </summary>
public class ChatService : IChatService
{
    private readonly IAppSettingsStore settingsStore;
    private readonly HttpClient httpClient;

    /// <summary>
    /// 默认 AI 人设提示词。用户可在设置中自定义覆盖。
    /// </summary>
    private static readonly string DefaultPersonaPrompt =
        "你是一个温和、会倾听、会整理事项的 AI 心灵伙伴。说话简洁、友好、有共情，优先帮用户把事情理清。";

    /// <summary>
    /// 通用离线回复库，当关键词未匹配时随机选取一条。
    /// 共 20 条，覆盖鼓励、共情、建议等语气。
    /// </summary>
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

    /// <summary>
    /// 系统级提示词，定义 AI 的行为边界和回复风格。
    /// 会被注入到所有 AI 请求的 system 消息中。
    /// </summary>
    private static readonly string ChatSystemPrompt =
        "你是一个温和的陪伴助手，名叫MemoMind。你的目标是先共情，再给出轻量建议。" +
        "不要进行诊断，不要进行说教，不要否定用户感受。" +
        "回复应自然、友好，使用鼓励式的语气。" +
        "如果用户表达了负面情绪，先承接情绪再表达理解，最后给出轻量建议。" +
        "如果用户提到了任务或计划，可以添加到其他模块对应的模块，同时温和地帮忙梳理。";

    /// <summary>
    /// 用于在 Agent 工具执行时创建 DI Scope（ChatService 本身是单例，无法直接注入 Scoped 服务）。
    /// </summary>
    private readonly IServiceScopeFactory scopeFactory;

    public ChatService(IAppSettingsStore settingsStore, IServiceScopeFactory scopeFactory)
    {
        this.settingsStore = settingsStore;
        this.scopeFactory = scopeFactory;
        httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    // ============================================================
    // IChatService 实现
    // ============================================================

    /// <summary>简单对话，使用默认系统提示词。</summary>
    public async Task<string> SendAsync(string inputText)
    {
        return await SendAsync(ChatSystemPrompt, inputText);
    }

    /// <summary>
    /// 带自定义系统提示词的对话。
    /// 如果 AI 未启用或 API Key 未配置，返回离线回复。
    /// </summary>
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

    /// <summary>带长期记忆的对话：记忆被拼接到系统提示词中。</summary>
    public async Task<string> SendAsync(string inputText, IReadOnlyList<string> memories)
    {
        var augmentedPrompt = BuildMemoryAugmentedPrompt(memories);
        return await SendAsync(augmentedPrompt, inputText);
    }

    /// <summary>带对话历史上下文的对话，用于保持多轮对话的连贯性。</summary>
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

    /// <summary>
    /// Agent 模式的主入口。
    ///
    /// 策略：
    /// 1. 优先使用原生 Function Calling（CallAiWithToolsAsync）
    /// 2. 如果 AI 返回的 tool_calls 中包含真实工具（非 diagnostic），直接返回
    /// 3. 如果 AI 没调用工具但用户输入是任务相关 → 回退到 JSON 指令模式
    /// 4. 如果 API 返回 400（不支持 Function Calling）+ 任务相关 → 回退 JSON 模式
    /// 5. 非任务相关的 400 → 回退到普通聊天
    /// </summary>
    public async Task<AgentResponse> SendAgentAsync(string inputText, IReadOnlyList<string> memories, IReadOnlyList<ChatHistoryItem> history)
    {
        var settings = await settingsStore.LoadAsync();
        var result = new AgentResponse { Reply = "", ToolResults = Array.Empty<AgentToolResult>() };

        if (!settings.EnableAi || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            result.Reply = GetOfflineResponse(inputText) + "\n\n（AI 功能不可用，请在设置中配置并启用 AI）";
            return result;
        }

        // 尝试原生 Function Calling
        try
        {
            var basePrompt = BuildMemoryAugmentedPrompt(memories);
            // 拼接工具定义和强制调用规则——这是 prompt 工程的核心：明确告诉 AI 何时必须调用工具
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
                          "\n## 计时与闹钟工具\n" +
                          "你可以调用以下函数来帮用户管理时间和闹钟：\n" +
                          "- start_pomodoro: 启动番茄钟（参数：work_minutes选填, break_minutes选填, cycles选填，不填则使用当前设置）\n" +
                          "- start_countdown: 启动倒计时（参数：hours选填, minutes选填, seconds选填，须至少指定一个大于0的值）\n" +
                          "- set_alarm: 设置闹钟（参数：hour必填0-23, minute必填0-59, name选填, message选填, repeat_mode选填once/daily/weekly）\n" +
                          "\n【必须遵守】\n" +
                          "用户说「创建/添加/新建/记一下/帮我安排」任务时 → 你必须调用 create_task\n" +
                          "用户说「查看/列出/有哪些/任务列表」 → 你必须调用 list_tasks\n" +
                          "用户说「完成/标记/修改/更新/改成」任务时 → 你必须调用 update_task\n" +
                          "用户说「删除/移除/取消/去掉」任务时 → 你必须调用 delete_task\n" +
                          "用户说「浇水/施肥/晒太阳/照顾植物/浇花」 → 你必须调用 care_plant\n" +
                          "用户说「植物怎么样/植物状态/看看植物/还好吗」 → 你必须调用 check_plant_status\n" +
                          "用户说「切换植物/换一棵/换到/去XX那边」 → 你必须调用 switch_plant\n" +
                          "用户说「有哪些植物/植物列表/看看植物」 → 你必须调用 list_plants\n" +
                          "用户说「开始番茄钟/开始专注/启动番茄/帮我番茄」 → 你必须调用 start_pomodoro\n" +
                          "用户说「倒计时/计时/帮我计个时」 → 你必须调用 start_countdown\n" +
                          "用户说「设个闹钟/提醒我/定个闹铃/帮我设一个X点的闹钟」 → 你必须调用 set_alarm\n" +
                          "不要只用文字说「已经帮你做了」却不调用工具！调用工具后根据实际结果回复。" +
                          "如果用户只是在倾诉心情、聊天，则不需要调用工具，正常回复即可。";

            var tools = Infrastructure.Services.AgentToolExecutor.GetAvailableTools();
            var nativeResult = await CallAiWithToolsAsync(settings, basePrompt, inputText, history, tools);

            // 如果原生工具确实被执行了，直接返回
            var realTools = new[] { "create_task", "list_tasks", "update_task", "delete_task", "care_plant", "check_plant_status", "switch_plant", "list_plants", "start_pomodoro", "start_countdown", "set_alarm" };
            if (nativeResult.ToolResults.Any(tr => realTools.Contains(tr.ToolName)))
                return nativeResult;

            // AI 没调用工具但用户可能想要操作任务 → 回退 JSON 指令模式
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
            // 模型不支持 Function Calling → 任务操作回退 JSON 指令模式
            if (IsTaskRelatedInput(inputText))
            {
                try { return await TryTextAgentAsync(settings, memories, inputText, history); }
                catch (Exception textEx)
                {
                    result.Reply = GetOfflineResponse(inputText) + $"\n\n（AI 调用失败：{textEx.Message}）";
                    return result;
                }
            }
            // 非任务聊天 → 回退到普通聊天
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

    // ============================================================
    // JSON 指令模式回退 (TryTextAgentAsync)
    // ============================================================

    /// <summary>
    /// 当原生 Function Calling 不可用时，要求 AI 以 JSON 格式返回操作指令。
    ///
    /// 要求格式：
    /// { "action": "create_task", "args": {...}, "reply": "自然语言回复" }
    ///
    /// 流程：构造 JSON 模式 prompt → 调用 AI → 解析 JSON → 执行工具 → 返回结果
    /// </summary>
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

        // 构造 JSON 指令 prompt：明确指定输出格式和所有可用操作
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
            "  计时：start_pomodoro, start_countdown, set_alarm\n" +
            "  无操作：none\n" +
            "create_task args: title(必填), description(选填), start_date(选填，格式yyyy-MM-dd HH:mm), due_date(选填，格式yyyy-MM-dd HH:mm), estimated_hours(选填，整数), estimated_minutes(选填，整数), is_urgent(选填，true/false)\n" +
            "update_task args: title(必填，用于查找原任务), new_title(选填), description(选填), status(选填Todo/Doing/Done), is_urgent(选填，true/false), start_date(选填), due_date(选填), estimated_hours(选填), estimated_minutes(选填)\n" +
            "delete_task args: title(必填，用于查找要删除的任务)\n" +
            "list_tasks args: {}\n" +
            "care_plant args: action(必填，water/fertilize/sunbathe), plant_type(选填)\n" +
            "check_plant_status args: plant_type(选填)\n" +
            "switch_plant args: plant_type(必填，植物中文名或英文id)\n" +
            "list_plants args: {}\n" +
            "start_pomodoro args: work_minutes(选填), break_minutes(选填), cycles(选填)\n" +
            "start_countdown args: hours(选填), minutes(选填), seconds(选填)，须至少一个大于0\n" +
            "set_alarm args: hour(必填，0-23), minute(必填，0-59), name(选填), message(选填), repeat_mode(选填，once/daily/weekly)\n" +
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

        // 清理 markdown 代码块包裹（模型有时会忽略"不要 markdown"的要求）
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

            // 优先使用 AI 生成的 reply 字段作为最终回复
            if (root.TryGetProperty("reply", out var replyProp))
                cleanedReply = replyProp.GetString() ?? rawReply;

            // 如果 action 不是 none/chat，执行对应的工具
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

                // 通过 ScopeFactory 创建 Scope，获取 Scoped 的 AgentToolExecutor
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

    /// <summary>字符串截断辅助方法，用于诊断日志。</summary>
    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return "(空)";
        return text.Length <= maxLen ? text : text[..maxLen] + "...";
    }

    // ============================================================
    // 原生 Function Calling (CallAiWithToolsAsync)
    // ============================================================

    /// <summary>
    /// 使用 OpenAI 兼容的 Function Calling 协议调用 AI。
    ///
    /// 流程：
    /// 1. 构建 messages（system + history + user），附带 tools 定义
    /// 2. 发送请求，tool_choice=auto 让 AI 自行决定是否调用工具
    /// 3. 如果 AI 返回 tool_calls → 本地执行每个工具 → 将结果作为 tool 消息追加
    ///    → 再次请求 AI 生成最终自然语言回复
    /// 4. 如果 AI 没有调用工具 → 直接返回文字回复
    ///
    /// DeepSeek 特殊处理：模型名需追加 |tools 后缀才能启用 Function Calling。
    /// </summary>
    private async Task<AgentResponse> CallAiWithToolsAsync(
        UserSettings settings,
        string systemPrompt,
        string userInput,
        IReadOnlyList<ChatHistoryItem> history,
        IReadOnlyList<ToolDefinition> tools)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl).TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-v4-flash" : settings.AiModel;
        // DeepSeek 的 Function Calling 需要在模型名后追加 |tools 后缀
        if (baseUrl.Contains("deepseek", StringComparison.OrdinalIgnoreCase) && !model.Contains('|'))
        {
            model += "|tools";
        }
        var finalSystemPrompt = BuildSystemPrompt(settings, systemPrompt);

        // 构建完整消息列表：system + 历史对话 + 当前用户输入
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
            tool_choice = "auto",     // AI 自行决定是否调用工具
            max_tokens = 500,
            temperature = 0.2         // 低温度以获得更确定的工具调用
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
        request.Content = content;

        var response = await httpClient.SendAsync(request);
        var statusCode = (int)response.StatusCode;

        // API 返回错误 → 返回诊断信息由上层决定回退策略
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
            // 非任务聊天 → 静默回退到普通聊天
            try
            {
                var fallbackReply = await CallAiWithHistoryAsync(settings, systemPrompt, userInput, history ?? Array.Empty<ChatHistoryItem>());
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

        // 检查 AI 是否调用了工具
        if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
        {
            var toolResults = new List<AgentToolResult>();

            // 将 assistant 的 tool_calls 消息追加到对话历史（OpenAI 协议要求）
            messages.Add(new
            {
                role = "assistant",
                content = (string?)null,
                tool_calls = toolCalls
            });

            // 逐一执行 AI 请求的工具调用
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

                // 将工具执行结果以 tool 角色追加到对话历史
                messages.Add(new
                {
                    role = "tool",
                    tool_call_id = callId,
                    content = execResult
                });
            }

            // 再次请求 AI：将工具执行结果告知 AI，让它生成最终的自然语言回复
            var followUpBody = new
            {
                model,
                messages,
                max_tokens = 500,
                temperature = 0.8     // 最终回复使用较高温度以增加语言多样性
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

        // AI 选择不调用任何工具 → 返回纯文字回复 + 诊断信息
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

    // ============================================================
    // 长期记忆提取 (ExtractMemoriesAsync)
    // ============================================================

    /// <summary>
    /// 从一轮对话中提取值得长期记住的用户信息。
    ///
    /// 要求 AI 分析「用户消息 + AI 回复」对，提取用户喜好、习惯、计划等，
    /// 返回 JSON 数组 [{"content": "...", "category": "喜好"}, ...]。
    ///
    /// 此方法由 ChatViewModel 在每次对话后以 fire-and-forget 方式调用，
    /// 失败静默——不影响主聊天体验。
    /// </summary>
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
            // 记忆提取失败静默处理——主回复已经展示给用户
            System.Diagnostics.Debug.WriteLine($"Memory extraction failed: {ex.Message}");
            return Array.Empty<ExtractedMemory>();
        }
    }

    // ============================================================
    // 底层 AI API 调用方法
    // ============================================================

    /// <summary>
    /// 最简单的 AI 调用：system prompt + 用户输入，无历史记录。
    /// 使用 temperature=0.8 以产生更自然多样的回复。
    /// </summary>
    private async Task<string> CallAiAsync(UserSettings settings, string systemPrompt, string userInput)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl).TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-v4-flash" : settings.AiModel;
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

    /// <summary>
    /// 带对话历史的 AI 调用：system prompt + 历史消息列表 + 用户输入。
    /// 历史消息以 role/content 格式拼接在 system 和 user 之间，
    /// 使 AI 能够参考之前的对话内容来维持上下文连贯性。
    /// </summary>
    private async Task<string> CallAiWithHistoryAsync(UserSettings settings, string systemPrompt, string userInput, IReadOnlyList<ChatHistoryItem> history)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl).TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-v4-flash" : settings.AiModel;
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

    /// <summary>
    /// JSON 模式 AI 调用：使用 response_format: json_object 约束 AI 输出纯 JSON。
    /// 使用 temperature=0.3 以降低 JSON 格式错误率。
    /// 用于 JSON 指令回退模式。
    /// </summary>
    private async Task<string> CallAiJsonModeAsync(UserSettings settings, string systemPrompt, string userInput, IReadOnlyList<ChatHistoryItem> history)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl).TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-v4-flash" : settings.AiModel;
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
            temperature = 0.3,                           // 低温度 → 更确定的 JSON 输出
            response_format = new { type = "json_object" } // 强制 JSON 输出
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

    // ============================================================
    // 系统提示词构建 (BuildSystemPrompt)
    // ============================================================

    /// <summary>
    /// 构建最终的 system prompt：基础提示词 + 用户自定义人设。
    /// 人设来自设置中的 AiPersona 字段，默认使用 DefaultPersonaPrompt。
    /// </summary>
    private static string BuildSystemPrompt(UserSettings settings, string systemPrompt)
    {
        var basePrompt = string.IsNullOrWhiteSpace(systemPrompt) ? ChatSystemPrompt : systemPrompt.Trim();
        var persona = string.IsNullOrWhiteSpace(settings.AiPersona) ? DefaultPersonaPrompt : settings.AiPersona.Trim();

        return basePrompt +
               "\n\n用户自定义的人设要求：" + persona +
               "\n请在不违背模块设定和安全要求的前提下，尽量体现该人设。";
    }

    // ============================================================
    // 离线回复系统 (GetOfflineResponse)
    // ============================================================

    /// <summary>
    /// AI 不可用时的离线回复策略。
    ///
    /// 两层匹配：
    /// 1. 先按关键词精确匹配情绪/场景（共 15 种），返回有针对性的温暖回复
    /// 2. 无匹配时从 20 条通用回复中随机选取一条
    ///
    /// 关键词匹配顺序会影响结果：更具体的情绪（如"焦虑"）排在前面。
    /// </summary>
    private static string GetOfflineResponse(string input)
    {
        var lower = input.ToLowerInvariant();

        // 按优先级匹配——更具体的情绪优先
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

        // 无匹配 → 随机通用回复
        var random = new Random();
        return OfflineResponses[random.Next(OfflineResponses.Length)];
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    /// <summary>
    /// 判断用户输入是否涉及任务/植物/计时器操作。
    /// 用于决定是否需要回退到 JSON 指令模式或切换到 Agent 管线。
    /// </summary>
    private static bool IsTaskRelatedInput(string input)
    {
        var taskKeywords = new[] { "创建", "添加", "新建", "任务", "记一下", "帮我安排", "查看", "列出", "有哪些",
                                   "完成", "标记", "修改", "更新", "改成", "删除", "移除", "取消", "去掉",
                                   "create", "add", "task", "todo", "done", "delete", "remove" };
        var plantKeywords = new[] { "浇水", "施肥", "晒太阳", "照顾植物", "浇花", "浇一下",
                                    "植物", "仙人掌", "向日葵", "薄荷", "蕨类", "竹子",
                                    "切换", "换一棵", "plant", "care" };
        var timerKeywords = new[] { "番茄钟", "番茄", "专注", "倒计时", "计时", "计个时",
                                    "闹钟", "闹铃", "提醒我", "设个", "定个", "timer", "alarm", "pomodoro" };
        return taskKeywords.Any(k => input.Contains(k, StringComparison.OrdinalIgnoreCase))
            || plantKeywords.Any(k => input.Contains(k, StringComparison.OrdinalIgnoreCase))
            || timerKeywords.Any(k => input.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 将用户的长期记忆拼接到系统提示词中。
    ///
    /// AI 会在回复时自然参考这些记忆（如用户喜欢喝咖啡、有只叫咪咪的猫），
    /// 但被明确要求"不要刻意逐条罗列"以避免机械感。
    /// </summary>
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

    /// <summary>
    /// 调用 AI API 进行记忆提取。
    ///
    /// 要求 AI 分析对话并返回 JSON 数组，每条包含：
    /// - content: 记忆内容（简洁中文描述）
    /// - category: 分类（喜好/兴趣/习惯/个人/计划/其他）
    ///
    /// 如果对话中无新信息则返回空数组。
    /// </summary>
    private async Task<IReadOnlyList<ExtractedMemory>> CallExtractionApiAsync(UserSettings settings, string userMessage, string aiReply)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl).TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-v4-flash" : settings.AiModel;

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
            temperature = 0.3    // 低温度提高提取一致性
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

        // 清理可能的 markdown 代码块包裹
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
            // 过滤掉空内容条目
            return extracted?.Where(m => !string.IsNullOrWhiteSpace(m.Content)).ToList()
                   ?? (IReadOnlyList<ExtractedMemory>)Array.Empty<ExtractedMemory>();
        }
        catch
        {
            return Array.Empty<ExtractedMemory>();
        }
    }
}
