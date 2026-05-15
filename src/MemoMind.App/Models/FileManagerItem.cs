using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MemoMind.App.Models;

public class FileManagerItem : INotifyPropertyChanged
{
    private bool isExpanded;
    private bool isChecked;

    public string DisplayName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public string Icon => IsFolder ? "\U0001F4C1" : "\U0001F4C4";
    public ObservableCollection<FileManagerItem> Children { get; } = [];
    public FileManagerItem? Parent { get; set; }

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded == value) return;
            isExpanded = value;
            OnPropertyChanged(nameof(IsExpanded));
        }
    }

    public bool IsChecked
    {
        get => isChecked;
        set
        {
            if (isChecked == value) return;
            isChecked = value;
            OnPropertyChanged(nameof(IsChecked));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
