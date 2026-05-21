using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MemoMind.App.ViewModels;

namespace MemoMind.App.Views;

/// <summary>
/// ChatView 的代码后置，负责 UI 行为协调。
///
/// 三个核心职责：
/// 1. ViewModel 切换时正确绑定/解绑事件（OnDataContextChanged）
/// 2. 新消息到达时自动滚动到底部
/// 3. 回车键快捷发送
///
/// 为什么需要代码后置：
/// - ScrollViewer 在 ListBox 的模板内部，纯 XAML 无法直接访问
/// - 需要通过 VisualTreeHelper 遍历可视化树来找到 ScrollViewer
/// </summary>
public partial class ChatView : UserControl
{
    private ChatViewModel? viewModel;

    public ChatView()
    {
        InitializeComponent();
        // 绑定 DataContext 变化事件，以便在 ViewModel 切换时重新注册事件
        DataContextChanged += OnDataContextChanged;
        // ListBox 首次加载完成后滚动到底部
        ChatListBox.Loaded += (_, _) => ScrollToBottom();
    }

    /// <summary>
    /// 当 ViewModel（DataContext）切换时：
    /// 1. 解绑旧 ViewModel 的事件（避免内存泄漏和重复触发）
    /// 2. 绑定新 ViewModel 的 ScrollToBottomRequested 事件
    /// 3. 监听新 ViewModel 的 Messages 集合的 CollectionChanged 事件
    /// </summary>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 清理旧绑定
        if (viewModel is not null)
        {
            viewModel.ScrollToBottomRequested -= ScrollToBottom;
            if (viewModel.Messages is INotifyCollectionChanged oldCollection)
                oldCollection.CollectionChanged -= OnMessagesChanged;
        }

        viewModel = DataContext as ChatViewModel;

        // 建立新绑定
        if (viewModel is not null)
        {
            viewModel.ScrollToBottomRequested += ScrollToBottom;
            if (viewModel.Messages is INotifyCollectionChanged collection)
                collection.CollectionChanged += OnMessagesChanged;
        }
    }

    /// <summary>
    /// 当 Messages 集合新增条目时自动滚动到底部。
    /// 这是最可靠的自动滚动时机——消息刚被添加到 UI 集合。
    /// </summary>
    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            ScrollToBottom();
    }

    /// <summary>
    /// 将 ListBox 的 ScrollViewer 滚动到最底部。
    ///
    /// 使用 Dispatcher.BeginInvoke + Background 优先级的原因：
    /// 消息刚添加到集合时，ListBox 可能还没有完成布局更新。
    /// 延迟到 Background 优先级确保新消息的 ItemContainer 已生成后再滚动。
    /// </summary>
    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // 通过可视化树查找 ListBox 内部的 ScrollViewer
            var scrollViewer = FindVisualChild<ScrollViewer>(ChatListBox);
            scrollViewer?.ScrollToEnd();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// 递归遍历可视化树，查找指定类型的子元素。
    /// ListBox 的 ScrollViewer 嵌套在其 ControlTemplate 内部，无法直接引用。
    /// </summary>
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

    /// <summary>
    /// 回车键快捷发送。
    /// 按下 Enter 时执行 SendCommand，效果等同于点击发送按钮。
    /// </summary>
    private void ChatInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ChatViewModel vm)
        {
            vm.SendCommand.Execute(null);
        }
    }
}
