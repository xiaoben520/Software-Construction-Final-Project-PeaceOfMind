using System.Collections.ObjectModel;

namespace MemoMind.App.Models;

public class FileManagerItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public string Icon => IsFolder ? "\U0001F4C1" : "\U0001F4C4";
    public ObservableCollection<FileManagerItem> Children { get; } = [];
}
