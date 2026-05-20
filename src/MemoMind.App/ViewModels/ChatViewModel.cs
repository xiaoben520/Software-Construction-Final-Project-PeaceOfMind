using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MemoMind.App.Commands;
using MemoMind.App.Models;
using MemoMind.App.Services;
using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MemoMind.App.ViewModels;

/// <summary>
/// 聊天页面的 ViewModel，是 View 和 Service 之间的桥梁。
///
/// 核心职责：
/// 1. 管理聊天消息列表（Messages）和输入状态（InputText / IsSending）
/// 2. 启动时从数据库加载历史聊天记录
/// 3. 用户发送消息时：保存 → 调用 AI → 展示 AI 回复 + 工具结果 → 提取长期记忆
/// 4. 向 View 暴露 ScrollToBottomRequested 事件，配合 ChatView 实现自动滚动
/// </summary>
public class ChatViewModel : ViewModelBase, ISettingsAwareViewModel, IPageLifecycleAware
{
    private readonly IChatService? chatService;
    private string inputText = string.Empty;
    private bool isSending;
    private string aiStatus = string.Empty;

    public ChatViewModel()
    {
        // IChatService 可能未注册（离线场景），因此用 GetService 而非 GetRequiredService
        chatService = App.Services.GetService<IChatService>();

        // 数据模板使用 DataTemplate，因此这里用普通类而非 ObservableObject 包装
        Messages = new ObservableCollection<ChatMessage>();

        // 只有当输入框有内容且未在发送中时，才允许点击发送
        SendCommand = new RelayCommand(_ => Send(), _ => !string.IsNullOrWhiteSpace(InputText) && !IsSending);

        // 同步读取设置以显示初始 AI 状态
        var settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
        var settings = settingsStore.LoadAsync().GetAwaiter().GetResult();
        ApplySettings(settings);

        // 从数据库加载历史聊天记录
        LoadHistory();
    }

    /// <summary>聊天消息列表，绑定到 ChatView 的 ListBox.ItemsSource</summary>
    public ObservableCollection<ChatMessage> Messages { get; }

    /// <summary>
    /// 输入框文本，双向绑定。
    /// 更新时触发 SendCommand 的 CanExecute 重新评估。
    /// </summary>
    public string InputText
    {
        get => inputText;
        set
        {
            inputText = value;
            OnPropertyChanged();
            (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// 是否正在等待 AI 回复。
    /// true 时：显示"正在输入..."提示，禁用发送按钮。
    /// </summary>
    public bool IsSending
    {
        get => isSending;
        set
        {
            isSending = value;
            OnPropertyChanged();
            (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// AI 状态提示文本，显示在聊天区域顶部。
    /// "✨ AI 模式已启用" 或 "💡 离线模式 — 在设置中配置 API Key 后可启用 AI 对话"
    /// </summary>
    public string AiStatus
    {
        get => aiStatus;
        set
        {
            aiStatus = value;
            OnPropertyChanged();
        }
    }

    /// <summary>发送按钮绑定的命令</summary>
    public ICommand SendCommand { get; }

    /// <summary>
    /// 通知 View 滚动到底部的事件。
    /// ChatView 代码后置订阅此事件，在新消息添加或页面切换时触发。
    /// </summary>
    public event Action? ScrollToBottomRequested;

    /// <summary>页面导航到此 ViewModel 时自动滚动到底部</summary>
    public Task OnNavigatedToAsync()
    {
        ScrollToBottomRequested?.Invoke();
        return Task.CompletedTask;
    }

    /// <summary>供外部调用的滚动请求（如主窗口切换标签页时）</summary>
    public void RequestScrollToBottom()
    {
        ScrollToBottomRequested?.Invoke();
    }

    /// <summary>
    /// 当用户在设置页修改 AI 配置后调用。
    /// 更新顶部状态栏的 AI 状态提示。
    /// </summary>
    public void ApplySettings(UserSettings settings)
    {
        AiStatus = settings.EnableAi && !string.IsNullOrWhiteSpace(settings.ApiKey)
            ? "✨ AI 模式已启用"
            : "💡 离线模式 — 在设置中配置 API Key 后可启用 AI 对话";
    }

    /// <summary>
    /// 从数据库加载历史聊天记录。
    /// 按时间升序添加到 Messages，若数据库为空则显示欢迎消息。
    /// 加载失败时静默回退到欢迎消息。
    /// </summary>
    private async void LoadHistory()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var chatMessageService = scope.ServiceProvider.GetRequiredService<IChatMessageService>();
            var history = await chatMessageService.GetAllAsync();

            foreach (var record in history)
            {
                Messages.Add(new ChatMessage
                {
                    Sender = record.Sender,
                    Content = record.Content,
                    Time = record.Time,
                    IsUserMessage = record.IsUserMessage
                });
            }
        }
        catch
        {
            // 数据库加载失败 → 显示欢迎消息（由下方的 Messages.Count == 0 逻辑处理）
        }

        if (Messages.Count == 0)
        {
            Messages.Add(new ChatMessage
            {
                Sender = "MemoMind",
                Content = "你好，我是你的 AI 心灵伙伴。我会帮你整理任务，也会温和地陪你说说话。",
                IsUserMessage = false
            });
        }
    }

    /// <summary>
    /// 将单条消息异步写入数据库。
    /// 使用 fire-and-forget 模式：保存失败不影响 UI 展示。
    /// </summary>
    private static async Task SaveMessageAsync(string sender, string content, bool isUserMessage)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var chatMessageService = scope.ServiceProvider.GetRequiredService<IChatMessageService>();
            await chatMessageService.AddAsync(new Core.Models.ChatMessageRecord
            {
                Sender = sender,
                Content = content,
                Time = DateTime.Now,
                IsUserMessage = isUserMessage
            });
        }
        catch
        {
            // 静默失败——消息已经在 UI 中展示
        }
    }

    /// <summary>
    /// 从 MemoryService 加载所有长期记忆的内容文本列表。
    /// 用于注入到 AI 的 system prompt 中。
    /// </summary>
    private static async Task<List<string>> LoadMemoryContentsAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var memoryService = scope.ServiceProvider.GetRequiredService<IMemoryService>();
            var memories = await memoryService.GetAllAsync();
            return memories.Select(m => m.Content).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// 获取最近的对话历史，用于发送给 AI 以维持上下文连贯性。
    ///
    /// 裁剪规则：
    /// - 最多取最近 10 条（超出上下文窗口无关，且节省 token）
    /// - 排除最新一条（即刚添加的当前用户输入，它已作为本轮 user 消息发送）
    /// - 逆序取再逆序回，保证时间升序
    /// </summary>
    private IReadOnlyList<ChatHistoryItem> GetRecentHistory()
    {
        const int maxHistory = 10;

        var historyMessages = Messages.Count <= 1
            ? Array.Empty<ChatMessage>()
            : Messages.Take(Messages.Count - 1).Reverse().Take(maxHistory).Reverse();

        return historyMessages
            .Select(m => new ChatHistoryItem
            {
                Role = m.IsUserMessage ? "user" : "assistant",
                Content = m.Content
            })
            .ToList();
    }

    /// <summary>
    /// 发送消息的核心流程。
    ///
    /// 步骤：
    /// 1. 添加用户消息到 UI + 异步保存到数据库
    /// 2. 清空输入框，设置 IsSending = true
    /// 3. 加载长期记忆 + 获取近期对话历史
    /// 4. 调用 chatService.SendAgentAsync 获取 AI 回复
    /// 5. 展示工具执行结果（系统消息）
    /// 6. 展示 AI 回复（MemoMind 消息）
    /// 7. 设置 IsSending = false
    /// 8. 异步提取长期记忆
    /// </summary>
    private async void Send()
    {
        var userText = InputText.Trim();
        if (string.IsNullOrWhiteSpace(userText) || IsSending)
        {
            return;
        }

        // 1. 添加用户消息
        var userTime = DateTime.Now;
        Messages.Add(new ChatMessage { Sender = "我", Content = userText, Time = userTime, IsUserMessage = true });
        _ = SaveMessageAsync("我", userText, true);

        InputText = string.Empty;
        IsSending = true;

        // 2. 调用 AI 服务
        string reply;
        IReadOnlyList<AgentToolResult> toolResults = Array.Empty<AgentToolResult>();
        if (chatService is not null)
        {
            var memories = await LoadMemoryContentsAsync();
            var history = GetRecentHistory();
            var agentResponse = await chatService.SendAgentAsync(userText, memories, history);
            reply = agentResponse.Reply;
            toolResults = agentResponse.ToolResults;
        }
        else
        {
            // 极端情况：IChatService 未注册 → 模拟延迟后给出离线回复
            await Task.Delay(400 + Random.Shared.Next(600));
            reply = "我收到了。先别急，我们可以把它拆成一两件最小的事。";
        }

        var replyTime = DateTime.Now;

        // 3. 展示工具执行结果（以"系统"发送者显示，✅/❌ 前缀）
        foreach (var tr in toolResults)
        {
            var icon = tr.Success ? "✅" : "❌";
            Messages.Add(new ChatMessage
            {
                Sender = "系统",
                Content = $"{icon} {tr.Message}",
                Time = replyTime,
                IsUserMessage = false
            });
            _ = SaveMessageAsync("系统", $"{icon} {tr.Message}", false);
        }

        // 4. 展示 AI 回复
        Messages.Add(new ChatMessage { Sender = "MemoMind", Content = reply, Time = replyTime, IsUserMessage = false });
        _ = SaveMessageAsync("MemoMind", reply, false);

        IsSending = false;

        // 5. 异步提取长期记忆（fire-and-forget，不影响主流程）
        if (chatService is not null)
        {
            _ = ExtractAndSaveMemoriesAsync(userText, reply);
        }
    }

    /// <summary>
    /// 从本轮对话中提取值得长期记住的信息并存入 MemoryService。
    ///
    /// fire-and-forget 模式：
    /// - 提取过程异步进行，不阻塞后续聊天
    /// - 失败静默丢弃，不影响主聊天体验
    /// - 同一内容重复提取时，MemoryService.AddOrUpdateAsync 会去重/合并
    /// </summary>
    private static async Task ExtractAndSaveMemoriesAsync(string userMessage, string aiReply)
    {
        try
        {
            var chatService = App.Services.GetService<IChatService>();
            if (chatService is null) return;

            var extracted = await chatService.ExtractMemoriesAsync(userMessage, aiReply);
            if (extracted.Count == 0) return;

            using var scope = App.Services.CreateScope();
            var memoryService = scope.ServiceProvider.GetRequiredService<IMemoryService>();
            foreach (var memory in extracted)
            {
                await memoryService.AddOrUpdateAsync(memory.Content, memory.Category);
            }
        }
        catch
        {
            // 记忆提取为 best-effort——静默失败
        }
    }
}
