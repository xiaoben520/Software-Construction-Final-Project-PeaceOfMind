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

public class ChatViewModel : ViewModelBase, ISettingsAwareViewModel, IPageLifecycleAware
{
    private readonly IChatService? chatService;
    private string inputText = string.Empty;
    private bool isSending;
    private string aiStatus = string.Empty;

    public ChatViewModel()
    {
        chatService = App.Services.GetService<IChatService>();
        Messages = new ObservableCollection<ChatMessage>();

        SendCommand = new RelayCommand(_ => Send(), _ => !string.IsNullOrWhiteSpace(InputText) && !IsSending);

        var settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
        var settings = settingsStore.LoadAsync().GetAwaiter().GetResult();
        ApplySettings(settings);

        LoadHistory();
    }

    public ObservableCollection<ChatMessage> Messages { get; }

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

    public string AiStatus
    {
        get => aiStatus;
        set
        {
            aiStatus = value;
            OnPropertyChanged();
        }
    }

    public ICommand SendCommand { get; }

    public event Action? ScrollToBottomRequested;

    public Task OnNavigatedToAsync()
    {
        ScrollToBottomRequested?.Invoke();
        return Task.CompletedTask;
    }

    public void RequestScrollToBottom()
    {
        ScrollToBottomRequested?.Invoke();
    }

    public void ApplySettings(UserSettings settings)
    {
        AiStatus = settings.EnableAi && !string.IsNullOrWhiteSpace(settings.ApiKey)
            ? "✨ AI 模式已启用"
            : "💡 离线模式 — 在设置中配置 API Key 后可启用 AI 对话";
    }

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
            // If DB load fails, show the welcome message below
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
            // Silently fail — message still shows in UI
        }
    }

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

    private IReadOnlyList<ChatHistoryItem> GetRecentHistory()
    {
        const int maxHistory = 10;

        // Exclude the most recent message (current user input just added) and take up to last 10
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

    private async void Send()
    {
        var userText = InputText.Trim();
        if (string.IsNullOrWhiteSpace(userText) || IsSending)
        {
            return;
        }

        var userTime = DateTime.Now;
        Messages.Add(new ChatMessage { Sender = "我", Content = userText, Time = userTime, IsUserMessage = true });
        _ = SaveMessageAsync("我", userText, true);

        InputText = string.Empty;
        IsSending = true;

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
            await Task.Delay(400 + Random.Shared.Next(600));
            reply = "我收到了。先别急，我们可以把它拆成一两件最小的事。";
        }

        var replyTime = DateTime.Now;

        // Show tool execution results as system messages
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

        Messages.Add(new ChatMessage { Sender = "MemoMind", Content = reply, Time = replyTime, IsUserMessage = false });
        _ = SaveMessageAsync("MemoMind", reply, false);

        IsSending = false;

        if (chatService is not null)
        {
            _ = ExtractAndSaveMemoriesAsync(userText, reply);
        }
    }

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
            // Memory extraction is best-effort — silent failure
        }
    }
}
