using System.Collections.ObjectModel;
using System.Windows.Input;
using MemoMind.App.Commands;
using MemoMind.App.Models;

namespace MemoMind.App.ViewModels;

public class ChatViewModel : ViewModelBase
{
    private string inputText = string.Empty;

    public ChatViewModel()
    {
        Messages = new ObservableCollection<ChatMessage>
        {
            new() { Sender = "MemoMind", Content = "你好，我会帮你整理任务，也会温和地陪你说说话。" }
        };

        SendCommand = new RelayCommand(_ => Send(), _ => !string.IsNullOrWhiteSpace(InputText));
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

    public ICommand SendCommand { get; }

    private void Send()
    {
        var userText = InputText.Trim();
        Messages.Add(new ChatMessage { Sender = "我", Content = userText });
        Messages.Add(new ChatMessage { Sender = "MemoMind", Content = "我收到了这句话。先别急，我们可以把它拆成一两件最小的事。" });
        InputText = string.Empty;
    }
}
