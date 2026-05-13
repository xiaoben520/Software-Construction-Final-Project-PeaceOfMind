using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using MemoMind.App.Commands;
using MemoMind.App.Models;
using MemoMind.App.Services;
using MemoMind.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace MemoMind.App.ViewModels;

public class FileWorkspaceViewModel : ViewModelBase, ISettingsAwareViewModel
{
    private readonly IFileWorkspaceStateService stateService;

    // --- Module visibility ---
    private bool showRecentFiles = true;
    private bool showWorkspaceGroups = true;
    private bool showFileManager = true;

    // --- Recent Files ---
    private RecentFileEntry? selectedRecentFile;
    private string recentStatus = string.Empty;

    // --- Workspace Groups ---
    private string newGroupName = string.Empty;
    private WorkspaceGroupViewModel? selectedWorkspaceGroup;
    private string workspaceStatus = string.Empty;

    // --- File Manager ---
    private string fileManagerRootPath = string.Empty;
    private FileManagerItem? selectedFileManagerItem;
    private string newFileName = string.Empty;
    private string fileManagerStatus = string.Empty;

    // --- Watchers ---
    private readonly List<FileSystemWatcher> activeWatchers = [];

    public FileWorkspaceViewModel()
        : this(App.Services.GetRequiredService<IFileWorkspaceStateService>())
    {
    }

    public FileWorkspaceViewModel(IFileWorkspaceStateService stateService)
    {
        this.stateService = stateService;

        RecentFiles = [];
        WorkspaceGroups = [];
        FileManagerTree = [];

        // Recent files commands
        OpenRecentFileCommand = new RelayCommand(_ => OpenRecentFile(), _ => SelectedRecentFile is not null);
        RemoveRecentFileCommand = new RelayCommand(_ => RemoveRecentFile(), _ => SelectedRecentFile is not null);
        ClearRecentFilesCommand = new RelayCommand(_ => ClearRecentFiles());

        // Workspace commands
        AddGroupCommand = new RelayCommand(_ => AddGroup(), _ => !string.IsNullOrWhiteSpace(NewGroupName));
        DeleteGroupCommand = new RelayCommand(param => DeleteGroup(param));
        AddItemToGroupCommand = new RelayCommand(param => AddItemToGroup(param));
        OpenWorkspaceItemCommand = new RelayCommand(param => OpenWorkspaceItem(param));
        RemoveWorkspaceItemCommand = new RelayCommand(param => RemoveWorkspaceItem(param));
        ToggleExpandCommand = new RelayCommand(param => ToggleExpand(param));

        // File manager commands
        BrowseRootPathCommand = new RelayCommand(_ => BrowseRootPath());
        OpenFileManagerItemCommand = new RelayCommand(_ => OpenFileManagerItem(), _ => SelectedFileManagerItem is not null);
        DeleteFileManagerItemCommand = new RelayCommand(_ => DeleteFileManagerItem(), _ => SelectedFileManagerItem is not null);
        RenameFileManagerItemCommand = new RelayCommand(_ => RenameFileManagerItem(), _ => SelectedFileManagerItem is not null && !string.IsNullOrWhiteSpace(NewFileName));
        AddNewFileCommand = new RelayCommand(_ => AddNewFile(), _ => !string.IsNullOrWhiteSpace(NewFileName) && !string.IsNullOrWhiteSpace(FileManagerRootPath));

        _ = LoadAsync();
    }

    // ==================== Properties ====================

    public bool ShowRecentFiles
    {
        get => showRecentFiles;
        set { showRecentFiles = value; OnPropertyChanged(); }
    }

    public bool ShowWorkspaceGroups
    {
        get => showWorkspaceGroups;
        set { showWorkspaceGroups = value; OnPropertyChanged(); }
    }

    public bool ShowFileManager
    {
        get => showFileManager;
        set { showFileManager = value; OnPropertyChanged(); }
    }

    // --- Recent Files ---
    public ObservableCollection<RecentFileEntry> RecentFiles { get; }

    public RecentFileEntry? SelectedRecentFile
    {
        get => selectedRecentFile;
        set
        {
            selectedRecentFile = value;
            OnPropertyChanged();
            (OpenRecentFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveRecentFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string RecentStatus
    {
        get => recentStatus;
        set { recentStatus = value; OnPropertyChanged(); }
    }

    // --- Workspace Groups ---
    public ObservableCollection<WorkspaceGroupViewModel> WorkspaceGroups { get; }

    public string NewGroupName
    {
        get => newGroupName;
        set
        {
            newGroupName = value;
            OnPropertyChanged();
            (AddGroupCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public WorkspaceGroupViewModel? SelectedWorkspaceGroup
    {
        get => selectedWorkspaceGroup;
        set
        {
            selectedWorkspaceGroup = value;
            OnPropertyChanged();
            (DeleteGroupCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddItemToGroupCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string WorkspaceStatus
    {
        get => workspaceStatus;
        set { workspaceStatus = value; OnPropertyChanged(); }
    }

    // --- File Manager ---
    public ObservableCollection<FileManagerItem> FileManagerTree { get; }

    public string FileManagerRootPath
    {
        get => fileManagerRootPath;
        set
        {
            fileManagerRootPath = value;
            OnPropertyChanged();
            (AddNewFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public FileManagerItem? SelectedFileManagerItem
    {
        get => selectedFileManagerItem;
        set
        {
            selectedFileManagerItem = value;
            OnPropertyChanged();
            (OpenFileManagerItemCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteFileManagerItemCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RenameFileManagerItemCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string NewFileName
    {
        get => newFileName;
        set
        {
            newFileName = value;
            OnPropertyChanged();
            (RenameFileManagerItemCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddNewFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string FileManagerStatus
    {
        get => fileManagerStatus;
        set { fileManagerStatus = value; OnPropertyChanged(); }
    }

    // ==================== Commands ====================

    public ICommand OpenRecentFileCommand { get; }
    public ICommand RemoveRecentFileCommand { get; }
    public ICommand ClearRecentFilesCommand { get; }

    public ICommand AddGroupCommand { get; }
    public ICommand DeleteGroupCommand { get; }
    public ICommand AddItemToGroupCommand { get; }
    public ICommand OpenWorkspaceItemCommand { get; }
    public ICommand RemoveWorkspaceItemCommand { get; }
    public ICommand ToggleExpandCommand { get; }

    public ICommand BrowseRootPathCommand { get; }
    public ICommand OpenFileManagerItemCommand { get; }
    public ICommand DeleteFileManagerItemCommand { get; }
    public ICommand RenameFileManagerItemCommand { get; }
    public ICommand AddNewFileCommand { get; }

    // ==================== Settings ====================

    public void ApplySettings(UserSettings settings)
    {
        ShowRecentFiles = settings.ShowRecentFiles;
        ShowWorkspaceGroups = settings.ShowWorkspaceGroups;
        ShowFileManager = settings.ShowFileManager;

        if (!string.IsNullOrWhiteSpace(settings.FileManagerRootPath) &&
            string.IsNullOrWhiteSpace(FileManagerRootPath))
        {
            FileManagerRootPath = settings.FileManagerRootPath;
            RefreshFileManagerTree();
        }
    }

    // ==================== Initialization ====================

    private async Task LoadAsync()
    {
        CleanupWatchers();
        await LoadRecentFilesAsync();
        await LoadWorkspaceGroupsAsync();
        RecentStatus = $"已加载 {RecentFiles.Count} 条最近记录。";
        WorkspaceStatus = $"已加载 {WorkspaceGroups.Count} 个工作区分组。";
    }

    private async Task LoadRecentFilesAsync()
    {
        var entries = await stateService.LoadRecentFilesAsync();
        RecentFiles.Clear();
        foreach (var entry in entries)
        {
            RecentFiles.Add(entry);
        }
    }

    private async Task LoadWorkspaceGroupsAsync()
    {
        var groups = await stateService.LoadWorkspaceGroupsAsync();
        WorkspaceGroups.Clear();
        foreach (var group in groups)
        {
            var vm = new WorkspaceGroupViewModel { Name = group.Name };
            foreach (var rootPath in group.RootPaths)
            {
                AddRootItemToGroup(vm, rootPath);
            }
            WorkspaceGroups.Add(vm);
            SetupWatcherForGroup(vm, group);
        }
    }

    // ==================== Recent Files ====================

    private async void OpenRecentFile()
    {
        if (SelectedRecentFile is null) return;

        var path = SelectedRecentFile.Path;
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            RecentStatus = "路径已失效。";
            return;
        }

        OpenPath(path);
        await stateService.AddRecentFileAsync(path);
        await LoadRecentFilesAsync();
        RecentStatus = $"已打开：{SelectedRecentFile.DisplayName}";
    }

    private async void RemoveRecentFile()
    {
        if (SelectedRecentFile is null) return;
        RecentFiles.Remove(SelectedRecentFile);
        await stateService.SaveRecentFilesAsync(RecentFiles.ToList());
        RecentStatus = "已移除记录。";
    }

    private async void ClearRecentFiles()
    {
        RecentFiles.Clear();
        await stateService.SaveRecentFilesAsync([]);
        RecentStatus = "已清空最近打开记录。";
    }

    // ==================== Workspace Groups ====================

    private async void AddGroup()
    {
        var name = NewGroupName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            WorkspaceStatus = "请输入分组名称。";
            return;
        }

        if (WorkspaceGroups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            WorkspaceStatus = "该分组名称已存在。";
            return;
        }

        var vm = new WorkspaceGroupViewModel { Name = name };
        WorkspaceGroups.Add(vm);
        NewGroupName = string.Empty;
        await PersistWorkspaceGroupsAsync();
        WorkspaceStatus = $"已创建分组：{name}";
    }

    private async void DeleteGroup(object? parameter)
    {
        var group = parameter as WorkspaceGroupViewModel ?? SelectedWorkspaceGroup;
        if (group is null) return;

        var name = group.Name;
        WorkspaceGroups.Remove(group);
        if (SelectedWorkspaceGroup == group)
            SelectedWorkspaceGroup = null;
        await PersistWorkspaceGroupsAsync();
        WorkspaceStatus = $"已删除分组：{name}";
        _ = LoadAsync();
    }

    private async void AddItemToGroup(object? parameter)
    {
        var group = parameter as WorkspaceGroupViewModel ?? SelectedWorkspaceGroup;
        if (group is null) return;
        SelectedWorkspaceGroup = group;

        // Use folder browser dialog pattern
        var folderDialog = new OpenFolderDialog
        {
            Title = "选择要添加的文件夹（或取消后用文件对话框选择文件）"
        };

        if (folderDialog.ShowDialog() == true)
        {
            var folderPath = folderDialog.FolderName;
            AddRootItemToGroup(group, folderPath);
            await PersistWorkspaceGroupsAsync();
            await LoadAsync();
            WorkspaceStatus = "已添加文件夹到分组。";
            return;
        }

        var fileDialog = new OpenFileDialog
        {
            Title = "选择要添加的文件",
            CheckFileExists = true,
            Filter = "所有文件|*.*"
        };

        if (fileDialog.ShowDialog() == true)
        {
            AddRootItemToGroup(group, fileDialog.FileName);
            await PersistWorkspaceGroupsAsync();
            await LoadAsync();
            WorkspaceStatus = "已添加文件到分组。";
        }
    }

    private void AddRootItemToGroup(WorkspaceGroupViewModel group, string path)
    {
        var normalizedPath = path.Trim();
        var isFolder = Directory.Exists(normalizedPath);

        if (!isFolder && !File.Exists(normalizedPath))
        {
            return;
        }

        if (group.RootItems.Any(x => string.Equals(x.FullPath, normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var item = new WorkspaceItemViewModel
        {
            DisplayName = isFolder ? new DirectoryInfo(normalizedPath).Name : Path.GetFileName(normalizedPath),
            FullPath = normalizedPath,
            IsFolder = isFolder,
            ParentGroup = group,
            IsExpanded = false
        };

        group.RootItems.Add(item);
    }

    private async void OpenWorkspaceItem(object? parameter)
    {
        var path = parameter as string;
        if (parameter is WorkspaceItemViewModel item)
        {
            path = item.FullPath;
        }

        if (string.IsNullOrWhiteSpace(path)) return;
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            WorkspaceStatus = "路径已失效。";
            return;
        }

        OpenPath(path);
        await stateService.AddRecentFileAsync(path);
        await LoadRecentFilesAsync();
        WorkspaceStatus = $"已打开。";
    }

    private async void RemoveWorkspaceItem(object? parameter)
    {
        if (parameter is not WorkspaceItemViewModel item) return;
        if (item.ParentGroup is null) return;

        item.ParentGroup.RootItems.Remove(item);
        await PersistWorkspaceGroupsAsync();
        await LoadAsync();
        WorkspaceStatus = $"已移除：{item.DisplayName}";
    }

    private void ToggleExpand(object? parameter)
    {
        if (parameter is not WorkspaceItemViewModel item) return;
        if (!item.IsFolder) return;

        item.IsExpanded = !item.IsExpanded;

        if (item.IsExpanded && item.Children.Count == 0)
        {
            LoadChildren(item);
        }
    }

    private void LoadChildren(WorkspaceItemViewModel folderItem)
    {
        folderItem.Children.Clear();
        try
        {
            foreach (var dir in Directory.GetDirectories(folderItem.FullPath))
            {
                folderItem.Children.Add(new WorkspaceItemViewModel
                {
                    DisplayName = Path.GetFileName(dir),
                    FullPath = dir,
                    IsFolder = true,
                    ParentGroup = folderItem.ParentGroup
                });
            }

            foreach (var file in Directory.GetFiles(folderItem.FullPath))
            {
                folderItem.Children.Add(new WorkspaceItemViewModel
                {
                    DisplayName = Path.GetFileName(file),
                    FullPath = file,
                    IsFolder = false,
                    ParentGroup = folderItem.ParentGroup
                });
            }
        }
        catch
        {
            // Skip inaccessible folders
        }
    }

    private void SetupWatcherForGroup(WorkspaceGroupViewModel groupVm, WorkspaceGroup groupData)
    {
        foreach (var rootPath in groupData.RootPaths)
        {
            if (!Directory.Exists(rootPath)) continue;

            try
            {
                var watcher = new FileSystemWatcher(rootPath)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                };

                watcher.Created += (_, _) => RefreshGroupFromWatcher(groupVm);
                watcher.Deleted += (_, _) => RefreshGroupFromWatcher(groupVm);
                watcher.Renamed += (_, _) => RefreshGroupFromWatcher(groupVm);
                watcher.EnableRaisingEvents = true;
                activeWatchers.Add(watcher);
            }
            catch
            {
                // Skip paths we can't watch
            }
        }
    }

    private async void RefreshGroupFromWatcher(WorkspaceGroupViewModel groupVm)
    {
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await LoadAsync();
        });
    }

    private void CleanupWatchers()
    {
        foreach (var watcher in activeWatchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        activeWatchers.Clear();
    }

    private async Task PersistWorkspaceGroupsAsync()
    {
        var groups = WorkspaceGroups.Select(g => new WorkspaceGroup
        {
            Name = g.Name,
            RootPaths = g.RootItems.Select(item => item.FullPath).ToList()
        }).ToList();

        await stateService.SaveWorkspaceGroupsAsync(groups);
    }

    // ==================== File Manager ====================

    private void BrowseRootPath()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择文件管理器根目录"
        };

        if (dialog.ShowDialog() == true)
        {
            FileManagerRootPath = dialog.FolderName;
            RefreshFileManagerTree();
            FileManagerStatus = $"已加载目录：{FileManagerRootPath}";
        }
    }

    public void RefreshFileManagerTree()
    {
        FileManagerTree.Clear();
        if (string.IsNullOrWhiteSpace(FileManagerRootPath) || !Directory.Exists(FileManagerRootPath))
        {
            return;
        }

        BuildFileManagerTree(FileManagerTree, FileManagerRootPath);
    }

    private void BuildFileManagerTree(ObservableCollection<FileManagerItem> parentCollection, string path)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                var dirItem = new FileManagerItem
                {
                    DisplayName = Path.GetFileName(dir),
                    FullPath = dir,
                    IsFolder = true
                };
                parentCollection.Add(dirItem);
                BuildFileManagerTree(dirItem.Children, dir);
            }

            foreach (var file in Directory.GetFiles(path))
            {
                parentCollection.Add(new FileManagerItem
                {
                    DisplayName = Path.GetFileName(file),
                    FullPath = file,
                    IsFolder = false
                });
            }
        }
        catch
        {
            // Skip inaccessible directories
        }
    }

    private async void OpenFileManagerItem()
    {
        if (SelectedFileManagerItem is null) return;

        var path = SelectedFileManagerItem.FullPath;
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            FileManagerStatus = "路径已失效。";
            return;
        }

        OpenPath(path);
        await stateService.AddRecentFileAsync(path);
        await LoadRecentFilesAsync();
        FileManagerStatus = $"已打开：{SelectedFileManagerItem.DisplayName}";
    }

    private void DeleteFileManagerItem()
    {
        if (SelectedFileManagerItem is null) return;

        var path = SelectedFileManagerItem.FullPath;
        try
        {
            if (SelectedFileManagerItem.IsFolder)
            {
                Directory.Delete(path, true);
            }
            else
            {
                File.Delete(path);
            }
            FileManagerStatus = $"已删除：{SelectedFileManagerItem.DisplayName}";
            RefreshFileManagerTree();
        }
        catch (Exception ex)
        {
            FileManagerStatus = $"删除失败：{ex.Message}";
        }
    }

    private void RenameFileManagerItem()
    {
        if (SelectedFileManagerItem is null || string.IsNullOrWhiteSpace(NewFileName)) return;

        var oldPath = SelectedFileManagerItem.FullPath;
        var parentDir = Path.GetDirectoryName(oldPath);
        if (string.IsNullOrWhiteSpace(parentDir)) return;

        var newPath = Path.Combine(parentDir, NewFileName.Trim());

        try
        {
            if (SelectedFileManagerItem.IsFolder)
            {
                Directory.Move(oldPath, newPath);
            }
            else
            {
                File.Move(oldPath, newPath);
            }
            FileManagerStatus = $"已重命名为：{NewFileName}";
            NewFileName = string.Empty;
            RefreshFileManagerTree();
        }
        catch (Exception ex)
        {
            FileManagerStatus = $"重命名失败：{ex.Message}";
        }
    }

    private void AddNewFile()
    {
        if (string.IsNullOrWhiteSpace(NewFileName) || string.IsNullOrWhiteSpace(FileManagerRootPath)) return;

        var newPath = Path.Combine(FileManagerRootPath, NewFileName.Trim());
        try
        {
            File.Create(newPath).Close();
            FileManagerStatus = $"已创建文件：{NewFileName}";
            NewFileName = string.Empty;
            RefreshFileManagerTree();
        }
        catch (Exception ex)
        {
            FileManagerStatus = $"创建失败：{ex.Message}";
        }
    }

    // ==================== Helpers ====================

    private static void OpenPath(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
