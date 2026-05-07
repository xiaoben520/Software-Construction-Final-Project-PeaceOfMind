using System.Windows.Input;
using System.ComponentModel;
using MemoMind.App.Commands;
using MemoMind.App.Models;
using MemoMind.App.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace MemoMind.App.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsStore settingsStore;
    private MainViewModel? mainViewModel;
    private string apiKey = string.Empty;
    private string aiBaseUrl = "https://api.openai.com/v1";
    private string aiModel = "gpt-3.5-turbo";
    private string aiPersona = "你是一个温和、会倾听、会整理事项的 AI 心灵伙伴。说话简洁、友好、有共情，优先帮用户把事情理清。";
    private bool enableAi;
    private bool enableReminder = true;
    private int reminderHour = 20;
    private string theme = "Light";
    private string statusMessage = "设置将保存到本地配置文件。";
    private bool suppressLayoutPreview;

    public SettingsViewModel()
        : this(App.Services.GetRequiredService<IAppSettingsStore>())
    {
    }

    public SettingsViewModel(IAppSettingsStore settingsStore)
    {
        this.settingsStore = settingsStore;
        SidebarOptions = [];
        HomeOptions = [];
        SaveCommand = new RelayCommand(_ => Save());
        LoadCommand = new RelayCommand(_ => Load());
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
            theme = App.NormalizeTheme(value);
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

    public ICommand SaveCommand { get; }
    public ICommand LoadCommand { get; }

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

    private async Task LoadAsync()
    {
        var settings = await settingsStore.LoadAsync();
        ApiKey = settings.ApiKey;
        AiBaseUrl = string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "https://api.openai.com/v1" : settings.AiBaseUrl;
        AiModel = string.IsNullOrWhiteSpace(settings.AiModel) ? "gpt-3.5-turbo" : settings.AiModel;
        AiPersona = string.IsNullOrWhiteSpace(settings.AiPersona)
            ? "你是一个温和、会倾听、会整理事项的 AI 心灵伙伴。说话简洁、友好、有共情，优先帮用户把事情理清。"
            : settings.AiPersona;
        EnableAi = settings.EnableAi;
        EnableReminder = settings.EnableReminder;
        ReminderHour = settings.ReminderHour;
        Theme = App.NormalizeTheme(settings.Theme);
        ApplySavedVisibility(settings);
        RefreshSettingsAwarePages(settings);
        StatusMessage = "已从本地配置文件加载设置。";
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
            Theme = App.NormalizeTheme(Theme),
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
    }

    private void Save()
    {
        _ = SaveAsync();
    }

    private void Load()
    {
        _ = LoadAsync();
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
}
