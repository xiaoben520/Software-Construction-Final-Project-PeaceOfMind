using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using MemoMind.App.ViewModels;

namespace MemoMind.App.Views;

public partial class CyberPlantView : UserControl
{
    private INotifyCollectionChanged? messageCollection;

    public CyberPlantView()
    {
        InitializeComponent();
        Loaded += (_, _) => HookMessages();
        DataContextChanged += (_, _) => HookMessages();
    }

    private void PlantType_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Models.PlantListItem plantItem)
        {
            if (DataContext is CyberPlantViewModel vm)
            {
                vm.SelectPlantCommand.Execute(plantItem);
            }
        }
    }

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is CyberPlantViewModel vm)
        {
            vm.SendCommand.Execute(null);
        }
    }

    private void HookMessages()
    {
        if (messageCollection is not null)
        {
            messageCollection.CollectionChanged -= Messages_CollectionChanged;
        }

        if (DataContext is CyberPlantViewModel vm)
        {
            messageCollection = vm.Messages;
            messageCollection.CollectionChanged += Messages_CollectionChanged;
            ScrollToBottom();
        }
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (MessageListBox.Items.Count > 0)
            {
                MessageListBox.ScrollIntoView(MessageListBox.Items[^1]);
            }
        }, DispatcherPriority.Background);
    }

    private void SelectCustomImage_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CyberPlantViewModel vm) return;

        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            vm.TrySetCustomEditImageFromFile(dialog.FileName);
        }
    }
}
