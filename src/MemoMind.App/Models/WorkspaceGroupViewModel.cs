using System.Collections.ObjectModel;
using MemoMind.App.ViewModels;

namespace MemoMind.App.Models;

public class WorkspaceGroupViewModel : ViewModelBase
{
    private string name = string.Empty;

    public string Name
    {
        get => name;
        set { name = value; OnPropertyChanged(); }
    }

    public ObservableCollection<WorkspaceItemViewModel> RootItems { get; } = [];
}
