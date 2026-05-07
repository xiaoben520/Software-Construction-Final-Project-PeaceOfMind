using System.Windows.Controls;
using System.Windows.Input;
using MemoMind.App.ViewModels;

namespace MemoMind.App.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
    }

    private void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ChatViewModel vm)
        {
            vm.SendCommand.Execute(null);
        }
    }
}
