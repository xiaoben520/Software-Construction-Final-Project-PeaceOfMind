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
    private readonly IAppSettingsStore settingsStore;

    // --- Module visibility ---
    private bool showRecentFiles = true;
    private bool showWorkspaceGroups = true;
    private bool showFileManager = true;

    // --- Recent Files ---
    private RecentFileEntry? selectedRecentFile;
    private string recentStatus = string.Empty;
    private int recentFilesLimit = 50;
    private bool isRecentFilesExpanded = true;

    // --- Workspace Groups ---
    private string newGroupName = string.Empty;
    private WorkspaceGroupViewModel? selectedWorkspaceGroup;
    private string workspaceStatus = string.Empty;

    // --- File Manager ---
    private string selectedRootPath = string.Empty;
    private FileManagerItem? selectedFileManagerItem;
    private string newFileName = string.Empty;
    private string fileManagerStatus = string.Empty;
    private HashSet<string> expandedPaths = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> hiddenPaths = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> workspaceExpandedPaths = new(StringComparer.OrdinalIgnoreCase);

    // --- Watchers ---
    private readonly List<FileSystemWatcher> activeWatchers = [];

    public FileWorkspaceViewModel()
        : this(
            App.Services.GetRequiredService<IFileWorkspaceStateService>(),
            App.Services.GetRequiredService<IAppSettingsStore>())
    {
    }

    public FileWorkspaceViewModel(IFileWorkspaceStateService stateService, IAppSettingsStore settingsStore)
    {
        this.stateService = stateService;
        this.settingsStore = settingsStore;

        RecentFiles = [];
        WorkspaceGroups = [];
        FileManagerTree = [];
        FileManagerRootPaths = [];

        // Recent files commands
        OpenRecentFileCommand = new RelayCommand(_ => OpenRecentFile(), _ => SelectedRecentFile is not null);
        RemoveRecentFileCommand = new RelayCommand(_ => RemoveRecentFile(), _ => SelectedRecentFile is not null);
        ClearRecentFilesCommand = new RelayCommand(_ => ClearRecentFiles());
        ToggleRecentFilesCommand = new RelayCommand(_ => ToggleRecentFiles());

        // Workspace commands
        AddGroupCommand = new RelayCommand(_ => AddGroup(), _ => !string.IsNullOrWhiteSpace(NewGroupName));
        DeleteGroupCommand = new RelayCommand(param => DeleteGroup(param));
        AddItemToGroupCommand = new RelayCommand(param => AddItemToGroup(param));
        OpenWorkspaceItemCommand = new RelayCommand(param => OpenWorkspaceItem(param));
        RemoveWorkspaceItemCommand = new RelayCommand(param => RemoveWorkspaceItem(param));
        ToggleExpandCommand = new RelayCommand(param => ToggleExpand(param));
        ExpandAllCommand = new RelayCommand(param => ExpandAll(param));
        CollapseAllCommand = new RelayCommand(param => CollapseAll(param));

        // File manager commands
        AddRootPathCommand = new RelayCommand(_ => AddRootPath());
        RemoveRootPathCommand = new RelayCommand(param => RemoveRootPath(param));
        OpenFileManagerItemCommand = new RelayCommand(_ => OpenFileManagerItem(), _ => SelectedFileManagerItem is not null);
        DeleteFileManagerItemCommand = new RelayCommand(_ => DeleteFileManagerItem(), _ => SelectedFileManagerItem is not null);
        RenameFileManagerItemCommand = new RelayCommand(_ => RenameFileManagerItem(), _ => SelectedFileManagerItem is not null && !string.IsNullOrWhiteSpace(NewFileName));
        AddNewFileCommand = new RelayCommand(_ => AddNewFile(), _ => !string.IsNullOrWhiteSpace(NewFileName) && FileManagerRootPaths.Count > 0);
        DeleteSelectedItemsCommand = new RelayCommand(_ => DeleteSelectedItems(), _ => HasCheckedItems);
        OpenSelectedItemsCommand = new RelayCommand(_ => OpenSelectedItems(), _ => HasCheckedItems);
        HideSelectedItemCommand = new RelayCommand(_ => HideSelectedItem(), _ => SelectedFileManagerItem is not null);
        HideSelectedItemsCommand = new RelayCommand(_ => HideSelectedItems(), _ => HasCheckedItems);
        ShowAllCommand = new RelayCommand(_ => ShowAll());

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

    public int RecentFilesLimit
    {
        get => recentFilesLimit;
        set { recentFilesLimit = Math.Clamp(value, 5, 200); OnPropertyChanged(); }
    }

    public bool IsRecentFilesExpanded
    {
        get => isRecentFilesExpanded;
        set { isRecentFilesExpanded = value; OnPropertyChanged(); }
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
    public ObservableCollection<string> FileManagerRootPaths { get; }

    public string SelectedRootPath
    {
        get => selectedRootPath;
        set
        {
            selectedRootPath = value;
            OnPropertyChanged();
        }
    }

    public string FileManagerRootPath
    {
        get => FileManagerRootPaths.Count > 0 ? FileManagerRootPaths[0] : string.Empty;
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
            (HideSelectedItemCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

    private bool hasCheckedItems;
    public bool HasCheckedItems
    {
        get => hasCheckedItems;
        set { hasCheckedItems = value; OnPropertyChanged(); }
    }

    // ==================== Commands ====================

    public ICommand OpenRecentFileCommand { get; }
    public ICommand RemoveRecentFileCommand { get; }
    public ICommand ClearRecentFilesCommand { get; }
    public ICommand ToggleRecentFilesCommand { get; }

    public ICommand AddGroupCommand { get; }
    public ICommand DeleteGroupCommand { get; }
    public ICommand AddItemToGroupCommand { get; }
    public ICommand OpenWorkspaceItemCommand { get; }
    public ICommand RemoveWorkspaceItemCommand { get; }
    public ICommand ToggleExpandCommand { get; }
    public ICommand ExpandAllCommand { get; }
    public ICommand CollapseAllCommand { get; }

    public ICommand AddRootPathCommand { get; }
    public ICommand RemoveRootPathCommand { get; }
    public ICommand OpenFileManagerItemCommand { get; }
    public ICommand DeleteFileManagerItemCommand { get; }
    public ICommand RenameFileManagerItemCommand { get; }
    public ICommand AddNewFileCommand { get; }
    public ICommand DeleteSelectedItemsCommand { get; }
    public ICommand OpenSelectedItemsCommand { get; }
    public ICommand HideSelectedItemCommand { get; }
    public ICommand HideSelectedItemsCommand { get; }
    public ICommand ShowAllCommand { get; }

    // ==================== Settings ====================

    public void ApplySettings(UserSettings settings)
    {
        ShowRecentFiles = settings.ShowRecentFiles;
        ShowWorkspaceGroups = settings.ShowWorkspaceGroups;
        ShowFileManager = settings.ShowFileManager;
        RecentFilesLimit = settings.RecentFilesLimit > 0 ? settings.RecentFilesLimit : 50;

        var hasRootPaths = settings.FileManagerRootPaths is { Count: > 0 };
        if ((!string.IsNullOrWhiteSpace(settings.FileManagerRootPath) || hasRootPaths) &&
            FileManagerRootPaths.Count == 0)
        {
            // Migrate from single path to list
            if (hasRootPaths)
            {
                foreach (var p in settings.FileManagerRootPaths)
                {
                    if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                        FileManagerRootPaths.Add(p);
                }
            }
            else if (!string.IsNullOrWhiteSpace(settings.FileManagerRootPath) &&
                     Directory.Exists(settings.FileManagerRootPath))
            {
                FileManagerRootPaths.Add(settings.FileManagerRootPath);
            }

            expandedPaths = new HashSet<string>(settings.FileManagerExpandedPaths, StringComparer.OrdinalIgnoreCase);
            hiddenPaths = new HashSet<string>(settings.FileManagerHiddenPaths, StringComparer.OrdinalIgnoreCase);
            RefreshFileManagerTree();
        }
    }

    public async Task ResetWorkspaceGroupsAsync()
    {
        await stateService.SaveWorkspaceGroupsAsync([]);
        await LoadWorkspaceGroupsAsync();
        WorkspaceStatus = "保存工作区已恢复初始化。";
    }

    public async Task ResetAllAsync()
    {
        // Clear recent files
        RecentFiles.Clear();
        await stateService.SaveRecentFilesAsync([]);
        RecentStatus = string.Empty;

        // Clear workspace groups
        await stateService.SaveWorkspaceGroupsAsync([]);
        await LoadWorkspaceGroupsAsync();
        WorkspaceStatus = string.Empty;

        // Clear file manager state
        FileManagerRootPaths.Clear();
        expandedPaths.Clear();
        hiddenPaths.Clear();
        RefreshFileManagerTree();
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
        var limit = RecentFilesLimit > 0 ? RecentFilesLimit : 50;
        foreach (var entry in entries.Take(limit))
        {
            RecentFiles.Add(entry);
        }
    }

    private async Task LoadWorkspaceGroupsAsync()
    {
        // Save expanded paths before clearing
        workspaceExpandedPaths.Clear();
        foreach (var groupVm in WorkspaceGroups)
        {
            foreach (var rootItem in groupVm.RootItems)
            {
                CollectWorkspaceExpandedPaths(rootItem);
            }
        }

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

            // Restore expanded state
            foreach (var rootItem in vm.RootItems)
            {
                RestoreWorkspaceExpandedPaths(rootItem);
            }

            SetupWatcherForGroup(vm, group);
        }
    }

    private void CollectWorkspaceExpandedPaths(WorkspaceItemViewModel item)
    {
        if (item.IsExpanded)
            workspaceExpandedPaths.Add(item.FullPath);
        foreach (var child in item.Children)
        {
            CollectWorkspaceExpandedPaths(child);
        }
    }

    private void RestoreWorkspaceExpandedPaths(WorkspaceItemViewModel item)
    {
        if (!item.IsFolder) return;

        if (workspaceExpandedPaths.Contains(item.FullPath))
        {
            item.IsExpanded = true;
            if (item.Children.Count == 0)
                _ = LoadChildrenAsync(item);

            foreach (var child in item.Children)
            {
                RestoreWorkspaceExpandedPaths(child);
            }
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
        await stateService.AddRecentFileAsync(path, RecentFilesLimit);
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

    private void ToggleRecentFiles()
    {
        IsRecentFilesExpanded = !IsRecentFilesExpanded;
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
        await stateService.AddRecentFileAsync(path, RecentFilesLimit);
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

        if (item.IsExpanded)
        {
            workspaceExpandedPaths.Add(item.FullPath);
            if (item.Children.Count == 0)
            {
                _ = LoadChildrenAsync(item);
            }
        }
        else
        {
            workspaceExpandedPaths.Remove(item.FullPath);
        }
    }

    public void EnsureWorkspaceChildrenLoaded(WorkspaceItemViewModel item)
    {
        if (!item.IsFolder) return;
        if (item.Children.Count == 0)
        {
            _ = LoadChildrenAsync(item);
        }
        workspaceExpandedPaths.Add(item.FullPath);
    }

    private void ExpandAll(object? parameter)
    {
        var group = parameter as WorkspaceGroupViewModel;
        if (group is null) return;

        foreach (var rootItem in group.RootItems)
        {
            ExpandRecursive(rootItem);
        }
    }

    private void CollapseAll(object? parameter)
    {
        var group = parameter as WorkspaceGroupViewModel;
        if (group is null) return;

        foreach (var rootItem in group.RootItems)
        {
            CollapseRecursive(rootItem);
        }
    }

    private void ExpandRecursive(WorkspaceItemViewModel item)
    {
        if (!item.IsFolder) return;

        item.IsExpanded = true;
        workspaceExpandedPaths.Add(item.FullPath);
        if (item.Children.Count == 0)
        {
            _ = LoadChildrenAsync(item);
        }

        foreach (var child in item.Children)
        {
            ExpandRecursive(child);
        }
    }

    private static void CollapseRecursive(WorkspaceItemViewModel item)
    {
        if (!item.IsFolder) return;
        item.IsExpanded = false;

        foreach (var child in item.Children)
        {
            CollapseRecursive(child);
        }
    }

    private async Task LoadChildrenAsync(WorkspaceItemViewModel folderItem)
    {
        var fullPath = folderItem.FullPath;
        List<WorkspaceItemViewModel> children;
        try
        {
            children = await Task.Run(() =>
            {
                var list = new List<WorkspaceItemViewModel>();
                foreach (var dir in Directory.EnumerateDirectories(fullPath))
                {
                    list.Add(new WorkspaceItemViewModel
                    {
                        DisplayName = Path.GetFileName(dir),
                        FullPath = dir,
                        IsFolder = true,
                        ParentGroup = folderItem.ParentGroup
                    });
                }
                foreach (var file in Directory.EnumerateFiles(fullPath))
                {
                    list.Add(new WorkspaceItemViewModel
                    {
                        DisplayName = Path.GetFileName(file),
                        FullPath = file,
                        IsFolder = false,
                        ParentGroup = folderItem.ParentGroup
                    });
                }
                return list;
            });
        }
        catch
        {
            return;
        }

        folderItem.Children.Clear();
        foreach (var child in children)
            folderItem.Children.Add(child);
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

    private async void AddRootPath()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择要添加的目录"
        };

        if (dialog.ShowDialog() == true)
        {
            var path = dialog.FolderName;
            if (FileManagerRootPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                FileManagerStatus = "该目录已在列表中。";
                return;
            }

            FileManagerRootPaths.Add(path);
            SelectedRootPath = path;
            RefreshFileManagerTree();
            FileManagerStatus = $"已添加目录：{path}";
            await SaveFileManagerSettingsAsync();
        }
    }

    private async void RemoveRootPath(object? parameter)
    {
        var path = parameter as string ?? SelectedRootPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        FileManagerRootPaths.Remove(path);
        if (SelectedRootPath == path)
            SelectedRootPath = FileManagerRootPaths.Count > 0 ? FileManagerRootPaths[0] : string.Empty;
        RefreshFileManagerTree();
        FileManagerStatus = $"已移除目录：{path}";
        await SaveFileManagerSettingsAsync();
    }

    private async Task SaveFileManagerSettingsAsync()
    {
        var settings = await settingsStore.LoadAsync();
        settings.FileManagerRootPath = FileManagerRootPath;
        settings.FileManagerRootPaths = FileManagerRootPaths.ToList();
        settings.FileManagerExpandedPaths = expandedPaths.ToList();
        settings.FileManagerHiddenPaths = hiddenPaths.ToList();
        await settingsStore.SaveAsync(settings);
    }

    private async Task SaveExpandedPathsAsync()
    {
        var settings = await settingsStore.LoadAsync();
        settings.FileManagerExpandedPaths = expandedPaths.ToList();
        settings.FileManagerHiddenPaths = hiddenPaths.ToList();
        await settingsStore.SaveAsync(settings);
    }

    public async Task RefreshFileManagerTreeAsync()
    {
        CollectExpandedPaths(FileManagerTree);
        var checkedPaths = CollectCheckedPaths(FileManagerTree);

        FileManagerTree.Clear();
        SelectedFileManagerItem = null;

        if (FileManagerRootPaths.Count == 0)
            return;

        foreach (var rootPath in FileManagerRootPaths)
        {
            if (!Directory.Exists(rootPath)) continue;

            var rootItem = new FileManagerItem
            {
                DisplayName = new DirectoryInfo(rootPath).Name,
                FullPath = rootPath,
                IsFolder = true,
                IsExpanded = expandedPaths.Contains(rootPath)
            };
            rootItem.PropertyChanged += FileManagerItem_PropertyChanged;
            FileManagerTree.Add(rootItem);
            await BuildFileManagerTreeAsync(rootItem.Children, rootPath, rootItem);
        }

        RestoreExpandedState(FileManagerTree);
        RestoreCheckedState(FileManagerTree, checkedPaths);
        UpdateHasCheckedItems();
    }

    public void RefreshFileManagerTree() => _ = RefreshFileManagerTreeAsync();

    private void CollectExpandedPaths(IEnumerable<FileManagerItem> items)
    {
        foreach (var item in items)
        {
            if (item.IsExpanded)
                expandedPaths.Add(item.FullPath);
            CollectExpandedPaths(item.Children);
        }
    }

    private HashSet<string> CollectCheckedPaths(IEnumerable<FileManagerItem> items)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectCheckedPathsInternal(items, paths);
        return paths;
    }

    private void CollectCheckedPathsInternal(IEnumerable<FileManagerItem> items, HashSet<string> paths)
    {
        foreach (var item in items)
        {
            if (item.IsChecked)
                paths.Add(item.FullPath);
            CollectCheckedPathsInternal(item.Children, paths);
        }
    }

    private void RestoreExpandedState(IEnumerable<FileManagerItem> items)
    {
        foreach (var item in items)
        {
            if (expandedPaths.Contains(item.FullPath))
                item.IsExpanded = true;
            RestoreExpandedState(item.Children);
        }
    }

    private static void RestoreCheckedState(IEnumerable<FileManagerItem> items, HashSet<string> checkedPaths)
    {
        foreach (var item in items)
        {
            if (checkedPaths.Contains(item.FullPath))
                item.IsChecked = true;
            RestoreCheckedState(item.Children, checkedPaths);
        }
    }

    private async Task BuildFileManagerTreeAsync(ObservableCollection<FileManagerItem> parentCollection, string path, FileManagerItem? parent)
    {
        List<(string fullPath, bool isFolder)> entries;
        try
        {
            entries = await Task.Run(() =>
            {
                var list = new List<(string, bool)>();
                foreach (var dir in Directory.EnumerateDirectories(path))
                {
                    if (!hiddenPaths.Contains(dir))
                        list.Add((dir, true));
                }
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    if (!hiddenPaths.Contains(file))
                        list.Add((file, false));
                }
                return list;
            });
        }
        catch
        {
            return;
        }

        foreach (var (fullPath, isFolder) in entries)
        {
            if (isFolder)
            {
                var dirItem = new FileManagerItem
                {
                    DisplayName = Path.GetFileName(fullPath),
                    FullPath = fullPath,
                    IsFolder = true,
                    IsExpanded = expandedPaths.Contains(fullPath),
                    Parent = parent
                };
                dirItem.PropertyChanged += FileManagerItem_PropertyChanged;
                parentCollection.Add(dirItem);
                await BuildFileManagerTreeAsync(dirItem.Children, fullPath, dirItem);
            }
            else
            {
                var fileItem = new FileManagerItem
                {
                    DisplayName = Path.GetFileName(fullPath),
                    FullPath = fullPath,
                    IsFolder = false,
                    Parent = parent
                };
                fileItem.PropertyChanged += FileManagerItem_PropertyChanged;
                parentCollection.Add(fileItem);
            }
        }
    }

    private void FileManagerItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileManagerItem.IsChecked))
        {
            UpdateHasCheckedItems();
        }
        else if (e.PropertyName == nameof(FileManagerItem.IsExpanded))
        {
            if (sender is FileManagerItem item)
            {
                if (item.IsExpanded)
                    expandedPaths.Add(item.FullPath);
                else
                    expandedPaths.Remove(item.FullPath);
                _ = SaveExpandedPathsAsync();
            }
        }
    }

    private void UpdateHasCheckedItems()
    {
        HasCheckedItems = HasAnyCheckedItems(FileManagerTree);
        (DeleteSelectedItemsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSelectedItemsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HideSelectedItemsCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private static bool HasAnyCheckedItems(IEnumerable<FileManagerItem> items)
    {
        foreach (var item in items)
        {
            if (item.IsChecked) return true;
            if (HasAnyCheckedItems(item.Children)) return true;
        }
        return false;
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

        ExpandAncestors(SelectedFileManagerItem);

        OpenPath(path);
        await stateService.AddRecentFileAsync(path, RecentFilesLimit);
        await LoadRecentFilesAsync();
        FileManagerStatus = $"已打开：{SelectedFileManagerItem.DisplayName}";
    }

    private void ExpandAncestors(FileManagerItem item)
    {
        var ancestor = item.Parent;
        while (ancestor is not null)
        {
            ancestor.IsExpanded = true;
            ancestor = ancestor.Parent;
        }
    }

    private async void OpenSelectedItems()
    {
        var checkedItems = new List<FileManagerItem>();
        CollectCheckedItems(FileManagerTree, checkedItems);

        if (checkedItems.Count == 0) return;

        var count = 0;
        foreach (var item in checkedItems)
        {
            ExpandAncestors(item);

            var path = item.FullPath;
            if (!Directory.Exists(path) && !File.Exists(path)) continue;

            OpenPath(path);
            await stateService.AddRecentFileAsync(path, RecentFilesLimit);
            count++;
        }

        await LoadRecentFilesAsync();
        FileManagerStatus = $"已批量打开 {count} 个项目。";
    }

    private void DeleteFileManagerItem()
    {
        if (SelectedFileManagerItem is null) return;

        var path = SelectedFileManagerItem.FullPath;
        var displayName = SelectedFileManagerItem.DisplayName;
        var isFolder = SelectedFileManagerItem.IsFolder;

        var typeName = isFolder ? "文件夹" : "文件";
        var confirm = MessageBox.Show(
            $"确定要删除{typeName} \"{displayName}\" 吗？\n\n此操作将永久删除实际文件，不可撤销。",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            if (isFolder)
            {
                Directory.Delete(path, true);
            }
            else
            {
                File.Delete(path);
            }
            FileManagerStatus = $"已删除：{displayName}";
            RefreshFileManagerTree();
        }
        catch (Exception ex)
        {
            FileManagerStatus = $"删除失败：{ex.Message}";
        }
    }

    private void DeleteSelectedItems()
    {
        var checkedItems = new List<FileManagerItem>();
        CollectCheckedItems(FileManagerTree, checkedItems);

        if (checkedItems.Count == 0) return;

        var confirm = MessageBox.Show(
            $"确定要删除选中的 {checkedItems.Count} 个项目吗？\n\n此操作将永久删除实际文件，不可撤销。",
            "批量删除确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var deleted = 0;
        var errors = 0;
        foreach (var item in checkedItems)
        {
            try
            {
                if (item.IsFolder)
                    Directory.Delete(item.FullPath, true);
                else
                    File.Delete(item.FullPath);
                deleted++;
            }
            catch
            {
                errors++;
            }
        }

        FileManagerStatus = errors > 0
            ? $"已删除 {deleted} 个项目，{errors} 个失败。"
            : $"已删除 {deleted} 个项目。";
        RefreshFileManagerTree();
    }

    private async void HideSelectedItem()
    {
        if (SelectedFileManagerItem is null) return;

        var path = SelectedFileManagerItem.FullPath;
        var displayName = SelectedFileManagerItem.DisplayName;

        // Don't hide root path items
        if (FileManagerRootPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            FileManagerStatus = "不能隐藏根目录，请使用移除目录功能。";
            return;
        }

        hiddenPaths.Add(path);
        FileManagerStatus = $"已从显示中移除：{displayName}";
        await SaveExpandedPathsAsync();
        RefreshFileManagerTree();
    }

    private async void HideSelectedItems()
    {
        var checkedItems = new List<FileManagerItem>();
        CollectCheckedItems(FileManagerTree, checkedItems);

        if (checkedItems.Count == 0) return;

        var count = 0;
        foreach (var item in checkedItems)
        {
            if (FileManagerRootPaths.Contains(item.FullPath, StringComparer.OrdinalIgnoreCase))
                continue;
            hiddenPaths.Add(item.FullPath);
            count++;
        }

        FileManagerStatus = $"已从显示中移除 {count} 个项目。";
        await SaveExpandedPathsAsync();
        RefreshFileManagerTree();
    }

    private void ShowAll()
    {
        if (hiddenPaths.Count == 0)
        {
            FileManagerStatus = "没有隐藏的项目。";
            return;
        }

        hiddenPaths.Clear();
        _ = SaveExpandedPathsAsync();
        RefreshFileManagerTree();
        FileManagerStatus = "已恢复所有隐藏项目。";
    }

    private void CollectCheckedItems(IEnumerable<FileManagerItem> items, List<FileManagerItem> result)
    {
        foreach (var item in items)
        {
            if (item.IsChecked) result.Add(item);
            CollectCheckedItems(item.Children, result);
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
        if (string.IsNullOrWhiteSpace(NewFileName) || FileManagerRootPaths.Count == 0) return;

        var targetRoot = !string.IsNullOrWhiteSpace(SelectedRootPath) && FileManagerRootPaths.Contains(SelectedRootPath)
            ? SelectedRootPath
            : FileManagerRootPaths[0];

        var newPath = Path.Combine(targetRoot, NewFileName.Trim());
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
