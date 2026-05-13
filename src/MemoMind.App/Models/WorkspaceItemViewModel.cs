using System.Collections.ObjectModel;
using MemoMind.App.ViewModels;

namespace MemoMind.App.Models;

public class WorkspaceItemViewModel : ViewModelBase
{
    private string displayName = string.Empty;
    private string fullPath = string.Empty;
    private bool isFolder;
    private bool isExpanded;

    public string DisplayName
    {
        get => displayName;
        set { displayName = value; OnPropertyChanged(); }
    }

    public string FullPath
    {
        get => fullPath;
        set { fullPath = value; OnPropertyChanged(); }
    }

    public bool IsFolder
    {
        get => isFolder;
        set { isFolder = value; OnPropertyChanged(); OnPropertyChanged(nameof(Icon)); }
    }

    public bool IsExpanded
    {
        get => isExpanded;
        set { isExpanded = value; OnPropertyChanged(); }
    }

    public string Icon => IsFolder ? "\U0001F4C1" : "\U0001F4C4";

    public ObservableCollection<WorkspaceItemViewModel> Children { get; } = [];

    public WorkspaceGroupViewModel? ParentGroup { get; set; }
}
