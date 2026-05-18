using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MemoMind.App.Models;
using MemoMind.App.ViewModels;

namespace MemoMind.App.Views;

public partial class FileWorkspaceView : UserControl
{
    public FileWorkspaceView()
    {
        InitializeComponent();
    }

    private FileWorkspaceViewModel? ViewModel => DataContext as FileWorkspaceViewModel;

    private void WorkspaceItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not WorkspaceItemViewModel item) return;

        if (!item.IsFolder)
        {
            ViewModel?.OpenWorkspaceItemCommand.Execute(item);
        }
        // Folders are handled by TreeView expand/collapse via IsExpanded binding + Expanded event
    }

    private void WorkspaceTreeItem_Selected(object sender, RoutedEventArgs e)
    {
        if (sender is TreeViewItem tvi)
        {
            tvi.IsSelected = false;
        }
    }

    private void WorkspaceTreeItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem tvi && tvi.DataContext is WorkspaceItemViewModel item)
        {
            ViewModel?.EnsureWorkspaceChildrenLoaded(item);
        }
    }

    private void FileManagerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (ViewModel is null) return;
        ViewModel.SelectedFileManagerItem = e.NewValue as FileManagerItem;
    }

    private void FileManagerTreeItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem tvi && tvi.DataContext is FileManagerItem item)
        {
            item.IsExpanded = true;
        }
    }

    private void FileManagerTreeItem_Collapsed(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem tvi && tvi.DataContext is FileManagerItem item)
        {
            item.IsExpanded = false;
        }
    }

    private void RecentFile_Open_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as MenuItem;
        var contextMenu = menuItem?.Parent as ContextMenu;
        var border = contextMenu?.PlacementTarget as Border;
        if (border?.DataContext is RecentFileEntry entry)
        {
            ViewModel!.SelectedRecentFile = entry;
            ViewModel.OpenRecentFileCommand.Execute(null);
        }
    }

    private void RecentFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.SelectedRecentFile is null) return;
        ViewModel.OpenRecentFileCommand.Execute(null);
    }

    private void RecentFile_Remove_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = sender as MenuItem;
        var contextMenu = menuItem?.Parent as ContextMenu;
        var border = contextMenu?.PlacementTarget as Border;
        if (border?.DataContext is RecentFileEntry entry)
        {
            ViewModel!.SelectedRecentFile = entry;
            ViewModel.RemoveRecentFileCommand.Execute(null);
        }
    }

    private void InnerControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is DependencyObject depObj)
        {
            var innerScrollViewer = FindVisualChild<ScrollViewer>(depObj);
            if (innerScrollViewer != null)
            {
                if ((e.Delta > 0 && innerScrollViewer.VerticalOffset > 0) ||
                    (e.Delta < 0 && innerScrollViewer.VerticalOffset < innerScrollViewer.ScrollableHeight))
                {
                    // Inner ScrollViewer still has room to scroll — let it handle the event
                    return;
                }
            }
        }

        // Inner ScrollViewer at boundary or absent — scroll the outer ScrollViewer
        MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }
        return null;
    }
}
