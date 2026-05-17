using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using MemoMind.App.Commands;
using MemoMind.App.Models;
using MemoMind.App.Services;
using MemoMind.Core.Models;
using MemoMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using Microsoft.Win32;

namespace MemoMind.App.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsStore settingsStore;
    private MainViewModel? mainViewModel;
    private string apiKey = string.Empty;
    private bool isApiKeyVisible;
    private string aiBaseUrl = "https://api.openai.com/v1";
    private string aiModel = "deepseek-v4-flash";
    private string aiPersona = DefaultAiPersona;
    private bool enableAi;
    private string theme = "System";
    private string statusMessage = "设置将保存到本地配置文件。";
    private bool isDirty;
    private bool suppressLayoutPreview;
    private int selectedProviderIndex;
    private bool showRecentFiles = true;
    private bool showWorkspaceGroups = true;
    private bool showFileManager = true;
    private string fileManagerRootPath = string.Empty;
    private int recentFilesLimit = 50;

    // Sound & popup settings
    private bool pomodoroSoundEnabled = true;
    private bool alarmSoundEnabled = true;
    private bool countdownSoundEnabled = true;
    private bool pomodoroPopupEnabled = true;
    private bool alarmPopupEnabled = true;
    private bool countdownPopupEnabled = true;
    private bool useCustomSound;
    private string customSoundPath = string.Empty;
    private const string DefaultAiPersona = "你是一个温和、会倾听、会整理事项的 AI 心灵伙伴。说话简洁、友好、有共情，优先帮用户把事情理清。";

    public SettingsViewModel()
        : this(App.Services.GetRequiredService<IAppSettingsStore>())
    {
    }

    public SettingsViewModel(IAppSettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        SidebarOptions = [];
        HomeOptions = [];
        ThemeOptions = new ObservableCollection<string>(App.AllThemes);
        ProviderOptions = new ObservableCollection<AiProviderPreset>(AiProviderPreset.All);
        ModelOptions = new ObservableCollection<string>(AiProviderPreset.All[0].Models);
        SaveCommand = new RelayCommand(_ => Save());
        BrowseFileManagerRootCommand = new RelayCommand(_ => BrowseFileManagerRoot());
        ClearFileManagerRootCommand = new RelayCommand(_ => ClearFileManagerRoot());
        ResetAllCommand = new RelayCommand(_ => ResetAll());
        ResetTaskBoardCommand = new RelayCommand(_ => ResetTaskBoard());
        ResetAiPersonaCommand = new RelayCommand(_ => ResetAiPersona());
        ResetWorkspaceGroupsCommand = new RelayCommand(_ => ResetWorkspaceGroups());
        ResetCyberPlantCommand = new RelayCommand(_ => ResetCyberPlant());
        ClearDatabaseCommand = new RelayCommand(_ => ClearDatabase());
        BrowseSoundCommand = new RelayCommand(_ => BrowseSoundFile());
        ToggleApiKeyVisibilityCommand = new RelayCommand(_ => ToggleApiKeyVisibility());
        _ = LoadAsync();
    }

    public string ApiKeyDisplayText
    {
        get => isApiKeyVisible ? apiKey : MaskApiKey(apiKey);
        set
        {
            apiKey = value ?? string.Empty;
            OnPropertyChanged();
            isDirty = true;
        }
    }

    public bool IsApiKeyVisible
    {
        get => isApiKeyVisible;
        set
        {
            isApiKeyVisible = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ApiKeyDisplayText));
            OnPropertyChanged(nameof(ApiKeyToggleText));
            OnPropertyChanged(nameof(IsApiKeyReadOnly));
        }
    }

    public string ApiKeyToggleText => isApiKeyVisible ? "隐藏" : "显示";

    public bool IsApiKeyReadOnly => !isApiKeyVisible;

    public ICommand ToggleApiKeyVisibilityCommand { get; }

    public string AiBaseUrl
    {
        get => aiBaseUrl;
        set
        {
            aiBaseUrl = value;
            OnPropertyChanged();
            isDirty = true;
        }
    }

    public string AiModel
    {
        get => aiModel;
        set
        {
            aiModel = value;
            OnPropertyChanged();
            isDirty = true;
        }
    }

    public string AiPersona
    {
        get => aiPersona;
        set
        {
            aiPersona = value;
            OnPropertyChanged();
            isDirty = true;
        }
    }

    public bool EnableAi
    {
        get => enableAi;
        set
        {
            enableAi = value;
            OnPropertyChanged();
            isDirty = true;
        }
    }


    public string Theme
    {
        get => theme;
        set
        {
            theme = value;
            OnPropertyChanged();
            App.ApplyTheme(theme);
            isDirty = true;
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

    public bool HasUnsavedChanges => isDirty;

    public int SelectedProviderIndex
    {
        get => selectedProviderIndex;
        set
        {
            selectedProviderIndex = value;
            OnPropertyChanged();
            isDirty = true;

            if (value >= 0 && value < ProviderOptions.Count)
            {
                var preset = ProviderOptions[value];
                if (!string.IsNullOrWhiteSpace(preset.BaseUrl))
                {
                    AiBaseUrl = preset.BaseUrl;
                }

                ModelOptions.Clear();
                foreach (var model in preset.Models)
                {
                    ModelOptions.Add(model);
                }

                AiModel = preset.Models.Count > 0 ? preset.Models[0] : string.Empty;
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand BrowseFileManagerRootCommand { get; }
    public ICommand ClearFileManagerRootCommand { get; }
    public ICommand ResetAllCommand { get; }
    public ICommand ResetTaskBoardCommand { get; }
    public ICommand ResetAiPersonaCommand { get; }
    public ICommand ResetWorkspaceGroupsCommand { get; }
    public ICommand ResetCyberPlantCommand { get; }
    public ICommand ClearDatabaseCommand { get; }

    public ObservableCollection<string> ThemeOptions { get; }
    public ObservableCollection<AiProviderPreset> ProviderOptions { get; }
    public ObservableCollection<string> ModelOptions { get; }

    // Sound & popup properties
    public bool PomodoroSoundEnabled
    {
        get => pomodoroSoundEnabled;
        set { pomodoroSoundEnabled = value; OnPropertyChanged(); isDirty = true; }
    }

    public bool AlarmSoundEnabled
    {
        get => alarmSoundEnabled;
        set { alarmSoundEnabled = value; OnPropertyChanged(); isDirty = true; }
    }

    public bool CountdownSoundEnabled
    {
        get => countdownSoundEnabled;
        set { countdownSoundEnabled = value; OnPropertyChanged(); isDirty = true; }
    }

    public bool PomodoroPopupEnabled
    {
        get => pomodoroPopupEnabled;
        set { pomodoroPopupEnabled = value; OnPropertyChanged(); isDirty = true; }
    }

    public bool AlarmPopupEnabled
    {
        get => alarmPopupEnabled;
        set { alarmPopupEnabled = value; OnPropertyChanged(); isDirty = true; }
    }

    public bool CountdownPopupEnabled
    {
        get => countdownPopupEnabled;
        set { countdownPopupEnabled = value; OnPropertyChanged(); isDirty = true; }
    }

    public bool UseCustomSound
    {
        get => useCustomSound;
        set { useCustomSound = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCustomSoundPathVisible)); isDirty = true; }
    }

    public string CustomSoundPath
    {
        get => customSoundPath;
        set { customSoundPath = value ?? string.Empty; OnPropertyChanged(); isDirty = true; }
    }

    public int RecentFilesLimit
    {
        get => recentFilesLimit;
        set { recentFilesLimit = Math.Clamp(value, 5, 200); OnPropertyChanged(); isDirty = true; }
    }

    public bool ShowRecentFiles
    {
        get => showRecentFiles;
        set { showRecentFiles = value; OnPropertyChanged(); isDirty = true; }
    }

    public bool ShowWorkspaceGroups
    {
        get => showWorkspaceGroups;
        set { showWorkspaceGroups = value; OnPropertyChanged(); isDirty = true; }
    }

    public bool ShowFileManager
    {
        get => showFileManager;
        set { showFileManager = value; OnPropertyChanged(); isDirty = true; }
    }

    public string FileManagerRootPath
    {
        get => fileManagerRootPath;
        set { fileManagerRootPath = value ?? string.Empty; OnPropertyChanged(); isDirty = true; }
    }

    public bool IsCustomSoundPathVisible => useCustomSound;

    public ICommand BrowseSoundCommand { get; }

    public ObservableCollection<PageVisibilityOptionViewModel> SidebarOptions { get; }

    public ObservableCollection<PageVisibilityOptionViewModel> HomeOptions { get; }

    public void Configure(MainViewModel mainViewModel)
    {
        this.mainViewModel = mainViewModel;
        BuildVisibilityOptions();
        _ = LoadAsync();
    }

    public void SyncFromCurrentLayout()
    {
        if (mainViewModel is null)
        {
            return;
        }

        suppressLayoutPreview = true;
        try
        {
            var sidebarSet = mainViewModel.SidebarPages.Select(page => page.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var homeSet = mainViewModel.HomePages.Select(page => page.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var option in SidebarOptions)
            {
                option.IsSelected = option.IsLocked || sidebarSet.Contains(option.Id);
            }

            foreach (var option in HomeOptions)
            {
                option.IsSelected = homeSet.Contains(option.Id);
            }
        }
        finally
        {
            suppressLayoutPreview = false;
        }
    }

    private async Task LoadAsync()
    {
        var settings = await settingsStore.LoadAsync();
        apiKey = settings.ApiKey;
        isApiKeyVisible = false;
        OnPropertyChanged(nameof(ApiKeyDisplayText));
        OnPropertyChanged(nameof(IsApiKeyVisible));
        OnPropertyChanged(nameof(ApiKeyToggleText));
        AiBaseUrl = string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl;
        AiModel = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-v4-flash" : settings.AiModel;
        AiPersona = string.IsNullOrWhiteSpace(settings.AiPersona) ? DefaultAiPersona : settings.AiPersona;
        EnableAi = settings.EnableAi;
        Theme = string.IsNullOrWhiteSpace(settings.Theme) ? "System" : settings.Theme;
        ShowRecentFiles = settings.ShowRecentFiles;
        ShowWorkspaceGroups = settings.ShowWorkspaceGroups;
        ShowFileManager = settings.ShowFileManager;
        FileManagerRootPath = settings.FileManagerRootPath;
        RecentFilesLimit = settings.RecentFilesLimit > 0 ? settings.RecentFilesLimit : 50;

        // Sound & popup settings
        PomodoroSoundEnabled = settings.PomodoroSoundEnabled;
        AlarmSoundEnabled = settings.AlarmSoundEnabled;
        CountdownSoundEnabled = settings.CountdownSoundEnabled;
        PomodoroPopupEnabled = settings.PomodoroPopupEnabled;
        AlarmPopupEnabled = settings.AlarmPopupEnabled;
        CountdownPopupEnabled = settings.CountdownPopupEnabled;
        UseCustomSound = settings.UseCustomSound;
        CustomSoundPath = settings.CustomSoundPath ?? string.Empty;

        ApplySavedVisibility(settings);
        RefreshSettingsAwarePages(settings);

        SelectedProviderIndex = MatchProviderIndex(AiBaseUrl, AiModel);
        isDirty = false;
        StatusMessage = "已从本地配置文件加载设置。";
    }

    private int MatchProviderIndex(string baseUrl, string model)
    {
        var bestIndex = 0;
        var bestScore = 0;

        for (var i = 0; i < ProviderOptions.Count; i++)
        {
            var preset = ProviderOptions[i];
            var score = 0;
            if (!string.IsNullOrWhiteSpace(preset.BaseUrl) &&
                string.Equals(preset.BaseUrl.TrimEnd('/'), baseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }

            if (preset.Models.Any(m => string.Equals(m, model, StringComparison.OrdinalIgnoreCase)))
            {
                score += 5;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        if (bestScore == 0)
        {
            return ProviderOptions.Count - 1;
        }

        return bestIndex;
    }

    private async Task SaveAsync()
    {
        // Merge with current on-disk settings to preserve values updated by other VMs (e.g. FileWorkspace)
        var currentSettings = await settingsStore.LoadAsync();
        var resolvedRootPath = !string.IsNullOrWhiteSpace(FileManagerRootPath)
            ? FileManagerRootPath
            : currentSettings.FileManagerRootPath;
        var resolvedRootPaths = currentSettings.FileManagerRootPaths;
        var resolvedExpandedPaths = currentSettings.FileManagerExpandedPaths;
        var resolvedHiddenPaths = currentSettings.FileManagerHiddenPaths;

        var settings = new UserSettings
        {
            ApiKey = apiKey,
            AiBaseUrl = AiBaseUrl,
            AiModel = AiModel,
            AiPersona = AiPersona,
            EnableAi = EnableAi,
            Theme = Theme,
            ShowRecentFiles = ShowRecentFiles,
            ShowWorkspaceGroups = ShowWorkspaceGroups,
            ShowFileManager = ShowFileManager,
            RecentFilesLimit = RecentFilesLimit,
            FileManagerRootPath = resolvedRootPath,
            FileManagerRootPaths = resolvedRootPaths,
            FileManagerExpandedPaths = resolvedExpandedPaths,
            FileManagerHiddenPaths = resolvedHiddenPaths,
            PomodoroSoundEnabled = PomodoroSoundEnabled,
            AlarmSoundEnabled = AlarmSoundEnabled,
            CountdownSoundEnabled = CountdownSoundEnabled,
            PomodoroPopupEnabled = PomodoroPopupEnabled,
            AlarmPopupEnabled = AlarmPopupEnabled,
            CountdownPopupEnabled = CountdownPopupEnabled,
            UseCustomSound = UseCustomSound,
            CustomSoundPath = CustomSoundPath,
            SidebarPageIds = SidebarOptions.Where(option => option.IsSelected).Select(option => option.Id).ToList(),
            HomePageIds = HomeOptions.Where(option => option.IsSelected).Select(option => option.Id).ToList()
        };

        if (mainViewModel is not null)
        {
            await mainViewModel.ApplySettingsAsync(settings);
        }
        else
        {
            await settingsStore.SaveAsync(settings);
        }

        isDirty = false;
        StatusMessage = "设置已保存。";
    }

    private void Save()
    {
        _ = SaveAsync();
    }

    public void SaveCurrentSettings()
    {
        _ = SaveAsync();
    }

    public void ReloadCurrentSettings()
    {
        _ = LoadAsync();
    }

    private void BrowseFileManagerRoot()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择管理器默认目录",
            CheckFileExists = false,
            CheckPathExists = true,
            ValidateNames = false,
            FileName = "选择文件夹"
        };

        if (dialog.ShowDialog() == true)
        {
            var folder = Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                FileManagerRootPath = folder;
            }
        }
    }

    private void ClearFileManagerRoot()
    {
        FileManagerRootPath = string.Empty;
    }

    private void ResetAll()
    {
        var result = MessageBox.Show(
            "将清空任务看板、AI 聊天人设、文件工作区和赛博植物的所有数据并恢复默认设置。\n\n此操作不可撤销，确定继续？",
            "确认全部恢复初始化",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _ = ResetAllAsync();
    }

    private void ResetTaskBoard()
    {
        var result = MessageBox.Show(
            "将清空所有任务数据并载入默认示例任务，当前任务将无法恢复。\n\n确定继续？",
            "确认任务看板初始化",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _ = ResetTaskBoardInternalAsync();
    }

    private void ResetAiPersona()
    {
        _ = ResetAiPersonaAsync();
    }

    private void ResetWorkspaceGroups()
    {
        var result = MessageBox.Show(
            "将清空所有最近打开记录、保存工作区分组和小文件管理器数据，当前数据将无法恢复。\n\n确定继续？",
            "确认文件工作区初始化",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _ = ResetWorkspaceGroupsInternalAsync();
    }

    private void ResetCyberPlant()
    {
        _ = ResetCyberPlantAsync();
    }

    private void ClearDatabase()
    {
        _ = ClearDatabaseAsync();
    }

    private async Task ClearDatabaseAsync()
    {
        var result = MessageBox.Show(
            "清空数据库将删除所有任务、日历、情绪记录、文件工作区、番茄钟记录、植物数据和聊天记录。\n\n此操作不可撤销，确定继续？",
            "确认清空数据库",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var databasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MemoMind", "MemoMind.db");

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }

            using (var scope = App.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.Database.EnsureCreatedAsync();
            }

            using (var scope = App.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                SeedDefaultTasks(context);
                await context.SaveChangesAsync();
            }

            // Force task board to reload from the new database
            var taskBoard = GetTaskBoardViewModel();
            if (taskBoard is not null)
            {
                await taskBoard.ResetAndReloadAsync();
            }

            StatusMessage = "数据库已清空并重新创建，建议重新打开应用以确保所有功能正常。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"清空数据库失败: {ex.Message}";
        }
    }

    private async Task ResetAllAsync()
    {
        await ResetTaskBoardInternalAsync();
        await ResetAiPersonaAsync();
        await ResetWorkspaceGroupsInternalAsync();
        await ResetCyberPlantAsync();
        StatusMessage = "已完成全部恢复初始化。";
    }

    private async Task ResetTaskBoardInternalAsync()
    {
        try
        {
            var taskBoard = GetTaskBoardViewModel();
            if (taskBoard is not null)
            {
                await taskBoard.ResetAndReloadAsync();
            }

            StatusMessage = "任务看板已恢复初始化。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"任务看板初始化失败: {ex.Message}";
        }
    }

    private async Task ResetAiPersonaAsync()
    {
        try
        {
            var settings = await settingsStore.LoadAsync();
            settings.AiPersona = DefaultAiPersona;
            AiPersona = DefaultAiPersona;

            if (mainViewModel is not null)
            {
                await mainViewModel.ApplySettingsAsync(settings);
            }
            else
            {
                await settingsStore.SaveAsync(settings);
            }

            StatusMessage = "AI 聊天人设已恢复默认。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"AI 人设恢复失败: {ex.Message}";
        }
    }

    private async Task ResetWorkspaceGroupsInternalAsync()
    {
        try
        {
            var fileWorkspace = GetFileWorkspaceViewModel();
            if (fileWorkspace is not null)
            {
                await fileWorkspace.ResetAllAsync();
            }

            // Also clear file manager settings in UserSettings
            var currentSettings = await settingsStore.LoadAsync();
            currentSettings.FileManagerRootPaths = [];
            currentSettings.FileManagerExpandedPaths = [];
            currentSettings.FileManagerHiddenPaths = [];
            currentSettings.FileManagerRootPath = string.Empty;
            await settingsStore.SaveAsync(currentSettings);

            StatusMessage = "文件工作区已恢复初始化（已清空最近打开、保存工作区和小文件管理器数据）。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"文件工作区初始化失败: {ex.Message}";
        }
    }

    private async Task ResetCyberPlantAsync()
    {
        try
        {
            var cyberPlant = GetCyberPlantViewModel();
            if (cyberPlant is not null)
            {
                await cyberPlant.ResetAllDataAsync();
            }

            StatusMessage = "赛博植物已恢复默认设定。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"赛博植物恢复失败: {ex.Message}";
        }
    }

    private TaskBoardViewModel? GetTaskBoardViewModel()
        => mainViewModel?.AllPages.FirstOrDefault(page => page.PageViewModel is TaskBoardViewModel)?.PageViewModel as TaskBoardViewModel;

    private FileWorkspaceViewModel? GetFileWorkspaceViewModel()
        => mainViewModel?.AllPages.FirstOrDefault(page => page.PageViewModel is FileWorkspaceViewModel)?.PageViewModel as FileWorkspaceViewModel;

    private CyberPlantViewModel? GetCyberPlantViewModel()
        => mainViewModel?.AllPages.FirstOrDefault(page => page.PageViewModel is CyberPlantViewModel)?.PageViewModel as CyberPlantViewModel;

    private static void SeedDefaultTasks(AppDbContext dbContext)
    {
        if (dbContext.Tasks.Any())
        {
            return;
        }

        dbContext.Tasks.AddRange(
            new TaskItem
            {
                Title = "计网作业",
                Description = "完成课程作业并整理提交材料",
                DueDate = DateTime.Today.AddDays(2),
                IsUrgent = true,
                Status = "Todo",
                SourceType = "Seed"
            },
            new TaskItem
            {
                Title = "小组讨论",
                Description = "准备项目分工与展示内容",
                DueDate = DateTime.Today.AddDays(1),
                IsUrgent = false,
                Status = "Doing",
                SourceType = "Seed"
            });
    }

    private void BuildVisibilityOptions()
    {
        if (mainViewModel is null)
        {
            return;
        }

        foreach (var option in SidebarOptions)
        {
            option.PropertyChanged -= HandleOptionPropertyChanged;
        }

        foreach (var option in HomeOptions)
        {
            option.PropertyChanged -= HandleOptionPropertyChanged;
        }

        SidebarOptions.Clear();
        HomeOptions.Clear();

        foreach (var page in mainViewModel.AllPages)
        {
            SidebarOptions.Add(new PageVisibilityOptionViewModel
            {
                Id = page.Id,
                Title = page.Title,
                Description = page.Description,
                IsLocked = page.SidebarLocked,
                IsSelected = page.ShowInSidebar
            });

            HomeOptions.Add(new PageVisibilityOptionViewModel
            {
                Id = page.Id,
                Title = page.Title,
                Description = page.Description,
                IsLocked = false,
                IsSelected = page.ShowOnHome
            });
        }

        foreach (var option in SidebarOptions)
        {
            option.PropertyChanged += HandleOptionPropertyChanged;
        }

        foreach (var option in HomeOptions)
        {
            option.PropertyChanged += HandleOptionPropertyChanged;
        }
    }

    private void ApplySavedVisibility(UserSettings settings)
    {
        if (mainViewModel is null || SidebarOptions.Count == 0 || HomeOptions.Count == 0)
        {
            return;
        }

        var sidebarSet = (settings.SidebarPageIds ?? mainViewModel.SidebarPages.Select(page => page.Id).ToList())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var homeSet = (settings.HomePageIds ?? mainViewModel.HomePages.Select(page => page.Id).ToList())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        suppressLayoutPreview = true;
        try
        {
            foreach (var option in SidebarOptions)
            {
                option.IsSelected = option.IsLocked || sidebarSet.Contains(option.Id);
            }

            foreach (var option in HomeOptions)
            {
                option.IsSelected = homeSet.Contains(option.Id);
            }
        }
        finally
        {
            suppressLayoutPreview = false;
        }

        mainViewModel.ApplyLayout(
            SidebarOptions.Where(option => option.IsSelected).Select(option => option.Id),
            HomeOptions.Where(option => option.IsSelected).Select(option => option.Id));
    }

    private void HandleOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PageVisibilityOptionViewModel.IsSelected) || suppressLayoutPreview)
        {
            return;
        }

        isDirty = true;

        if (mainViewModel is null)
        {
            return;
        }

        mainViewModel.ApplyLayout(
            SidebarOptions.Where(option => option.IsSelected).Select(option => option.Id),
            HomeOptions.Where(option => option.IsSelected).Select(option => option.Id));
    }

    private void RefreshSettingsAwarePages(UserSettings settings)
    {
        if (mainViewModel is null)
        {
            return;
        }

        foreach (var page in mainViewModel.AllPages)
        {
            if (page.PageViewModel is ISettingsAwareViewModel settingsAwareViewModel)
            {
                settingsAwareViewModel.ApplySettings(settings);
            }
        }
    }

    private void ToggleApiKeyVisibility()
    {
        IsApiKeyVisible = !IsApiKeyVisible;
    }

    private static string MaskApiKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length <= 4)
            return key;

        return new string('●', Math.Min(key.Length - 4, 12)) + key[^4..];
    }

    private void BrowseSoundFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择自定义音效文件",
            Filter = "音频文件 (*.wav;*.mp3;*.wma)|*.wav;*.mp3;*.wma|WAV 文件 (*.wav)|*.wav|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            CustomSoundPath = dialog.FileName;
        }
    }
}
