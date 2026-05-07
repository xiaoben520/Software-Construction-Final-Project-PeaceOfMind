using System.Collections.ObjectModel;
using System.Windows.Input;
using MemoMind.App.Commands;
using MemoMind.App.Models;
using MemoMind.App.Services;
using MemoMind.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MemoMind.App.ViewModels;

public class ChatViewModel : ViewModelBase, ISettingsAwareViewModel
{
    private readonly IChatService? chatService;
    private string inputText = string.Empty;
    private bool isSending;
    private string aiStatus = string.Empty;

    public ChatViewModel()
    {
        chatService = App.Services.GetService<IChatService>();
        Messages = new ObservableCollection<ChatMessage>
        {
            new() { Sender = "MemoMind", Content = "你好，我是你的 AI 心灵伙伴。我会帮你整理任务，也会温和地陪你说说话。" }
        };

        SendCommand = new RelayCommand(_ => Send(), _ => !string.IsNullOrWhiteSpace(InputText) && !IsSending);

        var settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
        var settings = settingsStore.LoadAsync().GetAwaiter().GetResult();
        ApplySettings(settings);
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

    public void ApplySettings(UserSettings settings)
    {
        AiStatus = settings.EnableAi && !string.IsNullOrWhiteSpace(settings.ApiKey)
            ? "✨ AI 模式已启用"
            : "💡 离线模式 — 在设置中配置 API Key 后可启用 AI 对话";
    }

    private async void Send()
    {
        var userText = InputText.Trim();
        if (string.IsNullOrWhiteSpace(userText) || IsSending)
        {
            return;
        }

        Messages.Add(new ChatMessage { Sender = "我", Content = userText, Time = DateTime.Now });
        InputText = string.Empty;
        IsSending = true;

        string reply;
        if (chatService is not null)
        {
            reply = await chatService.SendAsync(userText);
        }
        else
        {
            await Task.Delay(400 + Random.Shared.Next(600));
            reply = "我收到了。先别急，我们可以把它拆成一两件最小的事。";
        }

        Messages.Add(new ChatMessage { Sender = "MemoMind", Content = reply, Time = DateTime.Now });
        IsSending = false;
    }
}
