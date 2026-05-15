using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MemoMind.App.ViewModels;

namespace MemoMind.App.Views;

public partial class ChatView : UserControl
{
    private ChatViewModel? viewModel;

    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ChatListBox.Loaded += (_, _) => ScrollToBottom();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (viewModel is not null)
        {
            viewModel.ScrollToBottomRequested -= ScrollToBottom;
            if (viewModel.Messages is INotifyCollectionChanged oldCollection)
                oldCollection.CollectionChanged -= OnMessagesChanged;
        }

        viewModel = DataContext as ChatViewModel;

        if (viewModel is not null)
        {
            viewModel.ScrollToBottomRequested += ScrollToBottom;
            if (viewModel.Messages is INotifyCollectionChanged collection)
                collection.CollectionChanged += OnMessagesChanged;
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(ChatListBox);
            scrollViewer?.ScrollToEnd();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found)
                return found;
            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
                return descendant;
        }
        return null;
    }

    private void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ChatViewModel vm)
        {
            vm.SendCommand.Execute(null);
        }
    }
}
