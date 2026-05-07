using System.Net.Http;
using System.Text;
using System.Text.Json;
using MemoMind.Core.Interfaces;
using MemoMind.App.Models;

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
        "回复应简短、自然、友好，使用鼓励式的语气。" +
        "如果用户表达了负面情绪，先承接情绪再表达理解，最后给出轻量建议。" +
        "如果用户提到了任务或计划，可以温和地帮忙梳理。" +
        "保持回复在100字以内。";

    public ChatService(IAppSettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
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
            return GetOfflineResponse(inputText);
        }

        try
        {
            return await CallAiAsync(settings, systemPrompt, inputText);
        }
        catch
        {
            return GetOfflineResponse(inputText) + "\n\n（提示：AI 服务暂时不可用，已切换到离线模式）";
        }
    }

    private async Task<string> CallAiAsync(UserSettings settings, string systemPrompt, string userInput)
    {
        var baseUrl = (string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl).TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(settings.AiModel) ? "gpt-3.5-turbo" : settings.AiModel;
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
}
