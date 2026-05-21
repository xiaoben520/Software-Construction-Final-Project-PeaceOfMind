using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using MemoMind.App.Commands;
using MemoMind.App.Models;
using MemoMind.App.Services;
using MemoMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace MemoMind.App.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsStore settingsStore;
    private MainViewModel? mainViewModel;
    private bool hasUnsavedChanges;
    private bool isLoading;
    private string apiKey = string.Empty;
    private string aiBaseUrl = "https://api.openai.com/v1";
    private string aiModel = "deepseek-chat";
    private string aiPersona = "你是一个温和、会倾听、会整理事项的 AI 心灵伙伴。说话简洁、友好、有共情，优先帮用户把事情理清。";
    private bool enableAi;
    private bool enableReminder = true;
    private int reminderHour = 20;
    private string theme = "System";
    private string statusMessage = "设置将保存到本地配置文件。";
    private bool suppressLayoutPreview;
    private int selectedProviderIndex;

    // Sound & popup settings
    private bool pomodoroSoundEnabled = true;
    private bool alarmSoundEnabled = true;
    private bool countdownSoundEnabled = true;
    private bool pomodoroPopupEnabled = true;
    private bool alarmPopupEnabled = true;
    private bool countdownPopupEnabled = true;
    private bool useCustomSound;
    private string customSoundPath = string.Empty;

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
        LoadCommand = new RelayCommand(_ => Load());
        DeleteDatabaseCommand = new RelayCommand(_ => DeleteDatabase());
        BrowseSoundCommand = new RelayCommand(_ => BrowseSoundFile());
        _ = LoadAsync();
    }

    public string ApiKey
    {
        get => apiKey;
        set
        {
            apiKey = value;
            OnPropertyChanged();
        }
    }

    public string AiBaseUrl
    {
        get => aiBaseUrl;
        set
        {
            aiBaseUrl = value;
            OnPropertyChanged();
        }
    }

    public string AiModel
    {
        get => aiModel;
        set
        {
            aiModel = value;
            OnPropertyChanged();
        }
    }

    public string AiPersona
    {
        get => aiPersona;
        set
        {
            aiPersona = value;
            OnPropertyChanged();
        }
    }

    public bool EnableAi
    {
        get => enableAi;
        set
        {
            enableAi = value;
            OnPropertyChanged();
        }
    }

    public bool EnableReminder
    {
        get => enableReminder;
        set
        {
            enableReminder = value;
            OnPropertyChanged();
        }
    }

    public int ReminderHour
    {
        get => reminderHour;
        set
        {
            reminderHour = Math.Clamp(value, 0, 23);
            OnPropertyChanged();
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

    public bool HasUnsavedChanges
    {
        get => hasUnsavedChanges;
        private set
        {
            hasUnsavedChanges = value;
            OnPropertyChanged();
        }
    }

    public int SelectedProviderIndex
    {
        get => selectedProviderIndex;
        set
        {
            selectedProviderIndex = value;
            OnPropertyChanged();

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
    public ICommand LoadCommand { get; }
    public ICommand DeleteDatabaseCommand { get; }

    public ObservableCollection<string> ThemeOptions { get; }
    public ObservableCollection<AiProviderPreset> ProviderOptions { get; }
    public ObservableCollection<string> ModelOptions { get; }

    // Sound & popup properties
    public bool PomodoroSoundEnabled
    {
        get => pomodoroSoundEnabled;
        set { pomodoroSoundEnabled = value; OnPropertyChanged(); }
    }

    public bool AlarmSoundEnabled
    {
        get => alarmSoundEnabled;
        set { alarmSoundEnabled = value; OnPropertyChanged(); }
    }

    public bool CountdownSoundEnabled
    {
        get => countdownSoundEnabled;
        set { countdownSoundEnabled = value; OnPropertyChanged(); }
    }

    public bool PomodoroPopupEnabled
    {
        get => pomodoroPopupEnabled;
        set { pomodoroPopupEnabled = value; OnPropertyChanged(); }
    }

    public bool AlarmPopupEnabled
    {
        get => alarmPopupEnabled;
        set { alarmPopupEnabled = value; OnPropertyChanged(); }
    }

    public bool CountdownPopupEnabled
    {
        get => countdownPopupEnabled;
        set { countdownPopupEnabled = value; OnPropertyChanged(); }
    }

    public bool UseCustomSound
    {
        get => useCustomSound;
        set { useCustomSound = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCustomSoundPathVisible)); }
    }

    public string CustomSoundPath
    {
        get => customSoundPath;
        set { customSoundPath = value ?? string.Empty; OnPropertyChanged(); }
    }

    public bool IsCustomSoundPathVisible => useCustomSound;

    public ICommand BrowseSoundCommand { get; }

    public ObservableCollection<PageVisibilityOptionViewModel> SidebarOptions { get; }

    public ObservableCollection<PageVisibilityOptionViewModel> HomeOptions { get; }

    public void Configure(MainViewModel mainViewModel)
    {
        this.mainViewModel = mainViewModel;
        BuildVisibilityOptions();
        Load();
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

    protected override void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (!isLoading && propertyName != nameof(HasUnsavedChanges) && propertyName != nameof(StatusMessage))
        {
            HasUnsavedChanges = true;
        }
    }

    private async Task LoadAsync()
    {
        isLoading = true;
        try
        {
            var settings = await settingsStore.LoadAsync();
            ApiKey = settings.ApiKey;
            AiBaseUrl = string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl;
            AiModel = string.IsNullOrWhiteSpace(settings.AiModel) ? "deepseek-chat" : settings.AiModel;
            AiPersona = string.IsNullOrWhiteSpace(settings.AiPersona)
                ? "你是一个温和、会倾听、会整理事项的 AI 心灵伙伴。说话简洁、友好、有共情，优先帮用户把事情理清。"
                : settings.AiPersona;
            EnableAi = settings.EnableAi;
            EnableReminder = settings.EnableReminder;
            ReminderHour = settings.ReminderHour;
            Theme = string.IsNullOrWhiteSpace(settings.Theme) ? "System" : settings.Theme;

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
            StatusMessage = "已从本地配置文件加载设置。";
        }
        finally
        {
            isLoading = false;
            HasUnsavedChanges = false;
        }
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
        var settings = new UserSettings
        {
            ApiKey = ApiKey,
            AiBaseUrl = AiBaseUrl,
            AiModel = AiModel,
            AiPersona = AiPersona,
            EnableAi = EnableAi,
            EnableReminder = EnableReminder,
            ReminderHour = ReminderHour,
            Theme = Theme,
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

        StatusMessage = "设置已保存。";
        HasUnsavedChanges = false;
    }

    private void Save()
    {
        _ = SaveAsync();
    }

    public void SaveCurrentSettings()
    {
        _ = SaveAsync();
    }

    private void Load()
    {
        _ = LoadAsync();
    }

    public void ReloadCurrentSettings()
    {
        _ = LoadAsync();
    }

    private async Task DeleteDatabaseAsync()
    {
        var confirm = MessageBox.Show(
            "这将删除本地数据库，所有任务等数据会被清空。是否继续？",
            "删除数据库",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            StatusMessage = "已取消删除数据库。";
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deleted = await dbContext.Database.EnsureDeletedAsync();
            StatusMessage = deleted
                ? "数据库已删除，请重启应用以重新生成。"
                : "未找到数据库或无需删除。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除数据库失败: {ex.Message}";
        }
    }

    private void DeleteDatabase()
    {
        _ = DeleteDatabaseAsync();
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
