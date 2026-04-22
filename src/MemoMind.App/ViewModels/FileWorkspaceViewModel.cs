using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using MemoMind.App.Commands;
using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MemoMind.App.ViewModels;

public class FileWorkspaceViewModel : ViewModelBase
{
    private readonly IFileWorkspaceService fileWorkspaceService;
    private string displayName = string.Empty;
    private string workspacePath = string.Empty;
    private string statusMessage = "可添加常用文件夹或文件路径。";
    private FileWorkspace? selectedWorkspace;

    public FileWorkspaceViewModel()
        : this(App.Services.GetRequiredService<IFileWorkspaceService>())
    {
    }

    public FileWorkspaceViewModel(IFileWorkspaceService fileWorkspaceService)
    {
        this.fileWorkspaceService = fileWorkspaceService;
        Workspaces = new ObservableCollection<FileWorkspace>();

        AddWorkspaceCommand = new RelayCommand(_ => AddWorkspace(), _ => !string.IsNullOrWhiteSpace(WorkspacePath));
        RemoveWorkspaceCommand = new RelayCommand(_ => RemoveWorkspace(), _ => SelectedWorkspace is not null);
        OpenWorkspaceCommand = new RelayCommand(_ => OpenWorkspace(), _ => SelectedWorkspace is not null);
        RefreshCommand = new RelayCommand(_ => Refresh());

        _ = LoadWorkspacesAsync();
    }

    public ObservableCollection<FileWorkspace> Workspaces { get; }

    public string DisplayName
    {
        get => displayName;
        set
        {
            displayName = value;
            OnPropertyChanged();
        }
    }

    public string WorkspacePath
    {
        get => workspacePath;
        set
        {
            workspacePath = value;
            OnPropertyChanged();
            (AddWorkspaceCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public FileWorkspace? SelectedWorkspace
    {
        get => selectedWorkspace;
        set
        {
            selectedWorkspace = value;
            OnPropertyChanged();
            (RemoveWorkspaceCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenWorkspaceCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        set
        {
            statusMessage = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddWorkspaceCommand { get; }
    public ICommand RemoveWorkspaceCommand { get; }
    public ICommand OpenWorkspaceCommand { get; }
    public ICommand RefreshCommand { get; }

    private async Task LoadWorkspacesAsync()
    {
        var items = await fileWorkspaceService.GetAllAsync();
        Workspaces.Clear();
        foreach (var item in items)
        {
            Workspaces.Add(item);
        }

        StatusMessage = $"已加载 {Workspaces.Count} 条工作区记录。";
    }

    private async void AddWorkspace()
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath))
        {
            return;
        }

        var normalizedPath = WorkspacePath.Trim();
        if (!Directory.Exists(normalizedPath) && !File.Exists(normalizedPath))
        {
            StatusMessage = "路径不存在，请检查后再添加。";
            return;
        }

        if (Workspaces.Any(x => string.Equals(x.Path, normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "该路径已存在。";
            return;
        }

        var workspace = new FileWorkspace
        {
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Path.GetFileName(normalizedPath) : DisplayName.Trim(),
            Path = normalizedPath,
            LastOpenedAt = null
        };

        await fileWorkspaceService.AddAsync(workspace);
        Workspaces.Insert(0, workspace);

        DisplayName = string.Empty;
        WorkspacePath = string.Empty;
        StatusMessage = "工作区已保存到数据库。";
    }

    private async void RemoveWorkspace()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        var id = SelectedWorkspace.Id;
        await fileWorkspaceService.DeleteAsync(id);
        Workspaces.Remove(SelectedWorkspace);
        SelectedWorkspace = null;
        StatusMessage = "已删除工作区。";
    }

    private async void OpenWorkspace()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        if (!Directory.Exists(SelectedWorkspace.Path) && !File.Exists(SelectedWorkspace.Path))
        {
            StatusMessage = "路径已失效，请删除或修改该记录。";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = SelectedWorkspace.Path,
            UseShellExecute = true
        });

        SelectedWorkspace.LastOpenedAt = DateTime.Now;
        await fileWorkspaceService.UpdateAsync(SelectedWorkspace);
        StatusMessage = "已打开对应路径。";
    }

    private void Refresh()
    {
        _ = LoadWorkspacesAsync();
    }
}
