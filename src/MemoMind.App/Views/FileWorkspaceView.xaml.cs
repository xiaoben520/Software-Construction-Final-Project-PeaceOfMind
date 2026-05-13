using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        if (item.IsFolder)
        {
            ViewModel?.ToggleExpandCommand.Execute(item);
        }
        else
        {
            ViewModel?.OpenWorkspaceItemCommand.Execute(item);
        }
    }

    private void FileManagerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (ViewModel is null) return;
        ViewModel.SelectedFileManagerItem = e.NewValue as FileManagerItem;
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
}
